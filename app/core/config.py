from pydantic_settings import BaseSettings
from typing import List

class Settings(BaseSettings):
    APP_NAME: str = "LinguaBot Arena"
    APP_VERSION: str = "1.0.0"
    DEBUG: bool = True
    SECRET_KEY: str = "changeme"

    GEMINI_API_KEY: str = ""
    GROQ_API_KEY: str = ""

    DATABASE_URL: str = "sqlite+aiosqlite:///./linguabot.db"

    ALLOWED_ORIGINS: str = "*"

    CHALLENGE_TIMEOUT_SECONDS: int = 30
    XP_CORRECT_ANSWER: int = 10
    XP_FAST_ANSWER_BONUS: int = 5
    XP_ARENA_WIN: int = 25
    XP_STREAK_BONUS: int = 15

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"

settings = Settings()