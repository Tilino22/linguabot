import asyncio
import random
import string
from fastapi import APIRouter, WebSocket, WebSocketDisconnect, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from pydantic import BaseModel
from typing import Dict, Optional, List
import json

from app.core.database import get_db
from app.models.game import ArenaRoom
from app.services.gemini_service import generate_challenge, evaluate_answer, generate_robot_message
from app.services.level_service import calculate_xp_reward
from app.routes.auth import get_current_user
from app.core.config import settings

router = APIRouter(prefix="/arena", tags=["arena"])


class PlayerConnection:
    def __init__(self, user_id: int, username: str, websocket: WebSocket):
        self.user_id = user_id
        self.username = username
        self.websocket = websocket
        self.score = 0
        self.eliminated = False

    async def send(self, data: dict):
        try:
            await self.websocket.send_text(json.dumps(data))
        except Exception:
            pass


class RoomManager:
    def __init__(self):
        self.rooms: Dict[str, dict] = {}

    def create(self, code: str, language: str, rounds: int, mode: str = "classic"):
        self.rooms[code] = {
            "code": code,
            "language": language,
            "total_rounds": rounds,
            "current_round": 0,
            "status": "waiting",
            "mode": mode,
            "players": [],
            "eliminated": [],
            "challenge": None,
            "answers": {},
            "scores": {},
        }

    def get(self, code: str) -> Optional[dict]:
        return self.rooms.get(code)

    def join(self, code: str, player: PlayerConnection) -> bool:
        room = self.rooms.get(code)
        if not room or room["status"] != "waiting":
            return False
        room["players"].append(player)
        room["scores"][player.user_id] = 0
        return True

    def leave(self, code: str, user_id: int):
        room = self.rooms.get(code)
        if room:
            room["players"] = [p for p in room["players"] if p.user_id != user_id]
            if not room["players"]:
                del self.rooms[code]

    def active_players(self, code: str) -> list:
        room = self.rooms.get(code)
        if not room:
            return []
        return [p for p in room["players"] if not p.eliminated]

    async def broadcast(self, code: str, data: dict, only_active: bool = False):
        room = self.rooms.get(code)
        if room:
            players = self.active_players(code) if only_active else room["players"]
            for player in players:
                try:
                    await player.send(data)
                except Exception:
                    pass

    async def send_to(self, code: str, user_id: int, data: dict):
        room = self.rooms.get(code)
        if room:
            for p in room["players"]:
                if p.user_id == user_id:
                    await p.send(data)

    def record_answer(self, code: str, user_id: int, answer: str, time: float):
        room = self.rooms.get(code)
        if room:
            room["answers"][user_id] = {"answer": answer, "time": time}

    def all_active_answered(self, code: str) -> bool:
        room = self.rooms.get(code)
        if not room:
            return False
        active = self.active_players(code)
        return all(p.user_id in room["answers"] for p in active)

    def clear_round(self, code: str):
        room = self.rooms.get(code)
        if room:
            room["answers"] = {}
            room["challenge"] = None


manager = RoomManager()


def make_code() -> str:
    return "".join(random.choices(string.ascii_uppercase + string.digits, k=6))


class CreateRoomRequest(BaseModel):
    language: str
    total_rounds: int = 5
    mode: str = "classic"


@router.post("/room/create")
async def create_room(
    body: CreateRoomRequest,
    token: str,
    db: AsyncSession = Depends(get_db),
):
    user = await get_current_user(token, db)
    code = make_code()

    room = ArenaRoom(
        room_code=code,
        player1_id=user.id,
        language=body.language,
        total_rounds=body.total_rounds,
    )
    db.add(room)
    await db.commit()

    manager.create(code, body.language, body.total_rounds, body.mode)

    return {
        "room_code": code,
        "language": body.language,
        "total_rounds": body.total_rounds,
        "mode": body.mode,
        "message": "Share this code with your opponents!"
    }


@router.get("/room/{code}")
async def get_room(code: str, db: AsyncSession = Depends(get_db)):
    result = await db.execute(select(ArenaRoom).where(ArenaRoom.room_code == code))
    room = result.scalar_one_or_none()
    if not room:
        raise HTTPException(status_code=404, detail="Room not found")

    mem_room = manager.get(code)
    player_count = len(mem_room["players"]) if mem_room else 0

    return {
        "room_code": room.room_code,
        "status": room.status,
        "language": room.language,
        "total_rounds": room.total_rounds,
        "mode": mem_room["mode"] if mem_room else "classic",
        "player_count": player_count,
    }


@router.post("/room/{code}/start")
async def start_room(code: str, token: str, db: AsyncSession = Depends(get_db)):
    user = await get_current_user(token, db)
    room = manager.get(code)
    if not room:
        raise HTTPException(status_code=404, detail="Room not found")
    if room["status"] != "waiting":
        raise HTTPException(status_code=400, detail="Room already started")
    if len(room["players"]) < 2:
        raise HTTPException(status_code=400, detail="Need at least 2 players")

    asyncio.create_task(start_game(code, room))
    return {"message": "Game starting!"}


@router.websocket("/ws/{code}/{user_id}/{username}")
async def arena_ws(
    websocket: WebSocket,
    code: str,
    user_id: int,
    username: str,
):
    await websocket.accept()

    room = manager.get(code)
    if not room:
        await websocket.send_text(json.dumps({"event": "error", "message": "Room not found"}))
        await websocket.close()
        return

    player = PlayerConnection(user_id, username, websocket)
    if not manager.join(code, player):
        await websocket.send_text(json.dumps({"event": "error", "message": "Cannot join room"}))
        await websocket.close()
        return

    await manager.broadcast(code, {
        "event": "player_joined",
        "username": username,
        "players": [p.username for p in room["players"]],
        "player_count": len(room["players"]),
        "is_host": len(room["players"]) == 1,
    })

    try:
        while True:
            data = await websocket.receive_text()
            msg = json.loads(data)

            if msg.get("event") == "submit_answer":
                if not player.eliminated:
                    manager.record_answer(code, user_id, msg["answer"], msg["response_time"])
                    await manager.send_to(code, user_id, {
                        "event": "answer_received",
                        "message": "⏳ Waiting for others...",
                    })
                    if manager.all_active_answered(code):
                        asyncio.create_task(evaluate_round(code, room))

            elif msg.get("event") == "start_game":
                if len(room["players"]) >= 2 and room["status"] == "waiting":
                    asyncio.create_task(start_game(code, room))

    except WebSocketDisconnect:
        manager.leave(code, user_id)
        await manager.broadcast(code, {
            "event": "player_disconnected",
            "username": username,
            "player_count": len(room.get("players", [])),
        })


async def start_game(code: str, room: dict):
    room["status"] = "countdown"
    await manager.broadcast(code, {"event": "game_starting", "message": "¡La batalla comienza!"})

    for i in range(3, 0, -1):
        await manager.broadcast(code, {"event": "countdown", "count": i})
        await asyncio.sleep(1)

    await next_round(code, room)


async def next_round(code: str, room: dict):
    active = manager.active_players(code)
    if len(active) <= 1:
        await finish_game(code, room)
        return

    room["current_round"] += 1
    room["status"] = "playing"
    manager.clear_round(code)

    challenge = await generate_challenge(room["language"], level=5)
    room["challenge"] = challenge

    await manager.broadcast(code, {
        "event": "round_start",
        "round": room["current_round"],
        "challenge": challenge,
        "time_limit": settings.CHALLENGE_TIMEOUT_SECONDS,
        "players_alive": [p.username for p in active],
        "players_alive_count": len(active),
    })

    await asyncio.sleep(settings.CHALLENGE_TIMEOUT_SECONDS)
    if room["status"] == "playing":
        asyncio.create_task(evaluate_round(code, room))


async def evaluate_round(code: str, room: dict):
    if room["status"] != "playing":
        return
    room["status"] = "round_end"

    challenge = room["challenge"]
    answers = room["answers"]
    active = manager.active_players(code)
    results = {}
    round_scores = {}

    for player in active:
        answer_data = answers.get(player.user_id, {"answer": "", "time": 30.0})
        evaluation = await evaluate_answer(
            challenge=challenge,
            player_answer=answer_data["answer"],
            language=room["language"],
            response_time_seconds=answer_data["time"],
        )
        round_score = evaluation["score"]
        room["scores"][player.user_id] += round_score
        round_scores[player.user_id] = round_score
        results[str(player.user_id)] = {
            "username": player.username,
            "evaluation": evaluation,
            "round_score": round_score,
            "total_score": room["scores"][player.user_id],
        }

    # Eliminar al jugador con menor puntaje en esta ronda
    if len(active) > 1:
        min_user_id = min(round_scores, key=lambda uid: round_scores[uid])
        eliminated_player = next((p for p in active if p.user_id == min_user_id), None)

        if eliminated_player:
            eliminated_player.eliminated = True
            room["eliminated"].append(eliminated_player.username)

            await manager.broadcast(code, {
                "event": "player_eliminated",
                "username": eliminated_player.username,
                "players_alive": [p.username for p in manager.active_players(code)],
                "players_alive_count": len(manager.active_players(code)),
            })

    await manager.broadcast(code, {
        "event": "round_result",
        "round": room["current_round"],
        "results": results,
        "correct_answer": challenge["correct_answer"],
        "players_alive": [p.username for p in manager.active_players(code)],
    })

    await asyncio.sleep(4)

    remaining = manager.active_players(code)
    if len(remaining) <= 1:
        await finish_game(code, room)
    else:
        await next_round(code, room)


async def finish_game(code: str, room: dict):
    room["status"] = "finished"
    remaining = manager.active_players(code)
    winner = remaining[0] if remaining else None

    await manager.broadcast(code, {
        "event": "game_over",
        "winner_username": winner.username if winner else "Empate",
        "final_scores": {str(uid): score for uid, score in room["scores"].items()},
        "eliminated_order": room["eliminated"],
    })