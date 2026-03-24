from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from pydantic import BaseModel
from typing import Optional
from datetime import datetime

from app.core.database import get_db
from app.models.user import User, GameSession
from app.services.gemini_service import generate_challenge, evaluate_answer, generate_session_report
from app.services.level_service import calculate_xp_reward, check_level_up
from app.routes.auth import get_current_user

router = APIRouter(prefix="/local", tags=["local-training"])


class StartSessionRequest(BaseModel):
    language: str
    level: Optional[int] = None
    token: Optional[str] = None


class AnswerRequest(BaseModel):
    session_id: int
    challenge_type: str
    question: str
    correct_answer: str
    player_answer: str
    response_time_seconds: float
    token: Optional[str] = None


class EndSessionRequest(BaseModel):
    session_id: int
    token: Optional[str] = None


@router.post("/session/start")
async def start_session(
    body: StartSessionRequest,
    db: AsyncSession = Depends(get_db),
):
    print(f"TOKEN RECIBIDO: '{body.token}'")  # ← agrega esto
    print(f"BODY COMPLETO: {body}")           # ← agrega esto
    user = await get_current_user(body.token or "", db)
    play_level = body.level or user.level

    session = GameSession(
        user_id=user.id,
        mode="local",
        language=body.language,
        level=play_level,
    )
    db.add(session)
    await db.commit()
    await db.refresh(session)

    challenge = await generate_challenge(body.language, play_level)

    return {
        "session_id": session.id,
        "level": play_level,
        "language": body.language,
        "challenge": challenge,
        "player_xp": user.xp,
        "player_level": user.level,
        "player_rank": user.rank,
    }


@router.post("/answer")
async def submit_answer(
    body: AnswerRequest,
    db: AsyncSession = Depends(get_db),
):
    user = await get_current_user(body.token or "", db)

    result = await db.execute(
        select(GameSession).where(
            GameSession.id == body.session_id,
            GameSession.user_id == user.id,
        )
    )
    session = result.scalar_one_or_none()
    if not session:
        raise HTTPException(status_code=404, detail="Session not found")

    challenge_dict = {
        "type": body.challenge_type,
        "question": body.question,
        "correct_answer": body.correct_answer,
    }
    evaluation = await evaluate_answer(
        challenge=challenge_dict,
        player_answer=body.player_answer,
        language=session.language,
        response_time_seconds=body.response_time_seconds,
    )

    new_streak = user.current_streak + 1 if evaluation["is_correct"] else 0
    xp_breakdown = calculate_xp_reward(
        is_correct=evaluation["is_correct"],
        partial_credit=evaluation.get("partial_credit", False),
        response_time=body.response_time_seconds,
        current_streak=new_streak,
        mode="local",
    )

    level_up_info = check_level_up(user.xp, xp_breakdown["total"], user.level)

    user.xp = level_up_info["total_xp"]
    user.level = level_up_info["new_level"]
    user.rank = level_up_info["new_rank"] or user.rank
    user.current_streak = new_streak
    if new_streak > user.best_streak:
        user.best_streak = new_streak

    session.score += evaluation["score"]
    if evaluation["is_correct"]:
        session.correct_answers += 1
    else:
        session.wrong_answers += 1
    session.xp_earned += xp_breakdown["total"]

    await db.commit()

    next_challenge = await generate_challenge(session.language, session.level)

    return {
        "evaluation": evaluation,
        "xp_breakdown": xp_breakdown,
        "level_up": level_up_info,
        "next_challenge": next_challenge,
        "current_streak": new_streak,
        "session_score": session.score,
    }


@router.post("/session/end")
async def end_session(
    body: EndSessionRequest,
    db: AsyncSession = Depends(get_db),
):
    user = await get_current_user(body.token or "", db)

    result = await db.execute(
        select(GameSession).where(
            GameSession.id == body.session_id,
            GameSession.user_id == user.id,
        )
    )
    session = result.scalar_one_or_none()
    if not session:
        raise HTTPException(status_code=404, detail="Session not found")

    session.ended_at = datetime.utcnow()
    user.total_games += 1
    await db.commit()

    total = session.correct_answers + session.wrong_answers

    report = await generate_session_report({
        "language": session.language,
        "level": session.level,
        "correct": session.correct_answers,
        "total": total,
        "avg_time": 10.0,
        "xp": session.xp_earned,
    })

    return {
        "session_summary": {
            "correct": session.correct_answers,
            "wrong": session.wrong_answers,
            "total": total,
            "accuracy": round(session.correct_answers / total * 100, 1) if total > 0 else 0,
            "xp_earned": session.xp_earned,
            "score": session.score,
        },
        "report": report,
        "player": {
            "level": user.level,
            "rank": user.rank,
            "xp": user.xp,
        }
    }