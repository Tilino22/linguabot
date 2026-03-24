from sqlalchemy import Column, Integer, String, DateTime, Float, ForeignKey
from sqlalchemy.orm import relationship
from sqlalchemy.sql import func
from app.core.database import Base


class User(Base):
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, index=True)
    username = Column(String(50), unique=True, index=True, nullable=False)
    email = Column(String(100), unique=True, index=True, nullable=False)
    hashed_password = Column(String, nullable=False)
    avatar_color = Column(String(7), default="#00FF88")

    # Progresión
    xp = Column(Integer, default=0)
    level = Column(Integer, default=1)
    rank = Column(String(20), default="Rookie")

    # Stats
    total_games = Column(Integer, default=0)
    arena_wins = Column(Integer, default=0)
    arena_losses = Column(Integer, default=0)
    best_streak = Column(Integer, default=0)
    current_streak = Column(Integer, default=0)

    created_at = Column(DateTime(timezone=True), server_default=func.now())

    # Relaciones
    game_sessions = relationship("GameSession", back_populates="user")


class GameSession(Base):
    __tablename__ = "game_sessions"

    id = Column(Integer, primary_key=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    mode = Column(String(10), nullable=False)
    language = Column(String(20), nullable=False)
    level = Column(Integer, default=1)

    score = Column(Integer, default=0)
    correct_answers = Column(Integer, default=0)
    wrong_answers = Column(Integer, default=0)
    xp_earned = Column(Integer, default=0)

    started_at = Column(DateTime(timezone=True), server_default=func.now())
    ended_at = Column(DateTime(timezone=True), nullable=True)

    user = relationship("User", back_populates="game_sessions")