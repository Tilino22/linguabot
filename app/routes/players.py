from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select, desc
from app.core.database import get_db
from app.models.user import User
from app.routes.auth import get_current_user

router = APIRouter(prefix="/players", tags=["players"])


@router.get("/leaderboard")
async def get_leaderboard(
    limit: int = 10,
    db: AsyncSession = Depends(get_db),
):
    result = await db.execute(
        select(User)
        .order_by(desc(User.xp))
        .limit(limit)
    )
    players = result.scalars().all()

    return {
        "leaderboard": [
            {
                "position": idx + 1,
                "username": p.username,
                "level": p.level,
                "rank": p.rank,
                "xp": p.xp,
                "arena_wins": p.arena_wins,
                "avatar_color": p.avatar_color,
                "win_rate": round(
                    p.arena_wins / (p.arena_wins + p.arena_losses) * 100, 1
                ) if (p.arena_wins + p.arena_losses) > 0 else 0,
            }
            for idx, p in enumerate(players)
        ]
    }


@router.get("/me")
async def get_my_profile(
    token: str,
    db: AsyncSession = Depends(get_db),
):
    user = await get_current_user(token, db)

    return {
        "id": user.id,
        "username": user.username,
        "level": user.level,
        "rank": user.rank,
        "xp": user.xp,
        "avatar_color": user.avatar_color,
        "stats": {
            "total_games": user.total_games,
            "arena_wins": user.arena_wins,
            "arena_losses": user.arena_losses,
            "best_streak": user.best_streak,
            "current_streak": user.current_streak,
            "win_rate": round(
                user.arena_wins / (user.arena_wins + user.arena_losses) * 100, 1
            ) if (user.arena_wins + user.arena_losses) > 0 else 0,
        },
    }


@router.put("/me/avatar")
async def update_avatar_color(
    color: str,
    token: str,
    db: AsyncSession = Depends(get_db),
):
    user = await get_current_user(token, db)
    user.avatar_color = color
    await db.commit()
    return {"message": "Avatar updated!", "avatar_color": color}