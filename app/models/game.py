from sqlalchemy import Column, Integer, String, DateTime, ForeignKey, JSON
from sqlalchemy.sql import func
from app.core.database import Base


class ArenaRoom(Base):
    __tablename__ = "arena_rooms"

    id = Column(Integer, primary_key=True)
    room_code = Column(String(8), unique=True, index=True)
    status = Column(String(15), default="waiting")

    player1_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    player2_id = Column(Integer, ForeignKey("users.id"), nullable=True)
    winner_id = Column(Integer, ForeignKey("users.id"), nullable=True)

    language = Column(String(20), nullable=False)
    total_rounds = Column(Integer, default=5)
    current_round = Column(Integer, default=0)

    player1_score = Column(Integer, default=0)
    player2_score = Column(Integer, default=0)

    rounds_data = Column(JSON, default=list)

    created_at = Column(DateTime(timezone=True), server_default=func.now())
    ended_at = Column(DateTime(timezone=True), nullable=True)