from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import FileResponse
from contextlib import asynccontextmanager

from app.core.config import settings
from app.core.database import init_db
from app.routes import auth, local_game, arena, players


@asynccontextmanager
async def lifespan(app: FastAPI):
    await init_db()
    print(f"✅ {settings.APP_NAME} v{settings.APP_VERSION} iniciado")
    yield
    print("👋 Servidor apagado")


app = FastAPI(
    title=settings.APP_NAME,
    version=settings.APP_VERSION,
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.mount("/static", StaticFiles(directory="static"), name="static")

app.include_router(auth.router)
app.include_router(local_game.router)
app.include_router(arena.router)
app.include_router(players.router)


@app.get("/")
async def root():
    return FileResponse("static/index.html")


@app.get("/health")
async def health():
    return {"status": "ok"}