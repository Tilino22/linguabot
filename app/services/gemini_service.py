from groq import Groq
import json
import random
from app.core.config import settings

client = Groq(api_key=settings.GROQ_API_KEY)

CHALLENGE_TYPES = [
    "translation", "fill_blank", "respond_in_language",
    "word_definition", "sentence_correction",
]

def get_difficulty(level: int) -> str:
    if level <= 5:  return "beginner"
    if level <= 15: return "elementary"
    if level <= 30: return "intermediate"
    if level <= 50: return "advanced"
    return "expert"

def call_groq(prompt: str) -> str:
    response = client.chat.completions.create(
        model="llama-3.1-8b-instant",
        messages=[{"role": "user", "content": prompt}],
        max_tokens=1024,
    )
    return response.choices[0].message.content.strip()

async def generate_challenge(language: str, level: int, challenge_type: str = None) -> dict:
    difficulty = get_difficulty(level)
    if not challenge_type:
        challenge_type = random.choice(CHALLENGE_TYPES)

    prompt = f"""You are the game engine for LinguaBot Arena.
Generate a single language challenge in JSON format.

Language: {language}
Difficulty: {difficulty} (level {level})
Challenge type: {challenge_type}

Return ONLY valid JSON:
{{
  "type": "{challenge_type}",
  "question": "The challenge text",
  "correct_answer": "The exact correct answer",
  "hint": "A subtle hint",
  "difficulty": "{difficulty}",
  "topic": "The topic category"
}}

Rules:
- translation: Spanish phrase to translate to {language}
- fill_blank: sentence with ___ where answer goes
- respond_in_language: question in {language} to answer in {language}
- word_definition: {language} word asking for its meaning
- sentence_correction: sentence with one grammar error to fix
Return ONLY JSON, no markdown."""

    raw = call_groq(prompt)
    raw = raw.replace("```json", "").replace("```", "").strip()
    return json.loads(raw)

async def evaluate_answer(challenge: dict, player_answer: str, language: str, response_time_seconds: float) -> dict:
    prompt = f"""Evaluate this language learning answer.

Challenge type: {challenge['type']}
Question: {challenge['question']}
Correct answer: {challenge['correct_answer']}
Player's answer: {player_answer}
Language: {language}
Response time: {response_time_seconds:.1f}s

Return ONLY valid JSON:
{{
  "is_correct": true or false,
  "partial_credit": true or false,
  "score": 0 to 100,
  "feedback": "Short encouraging feedback (max 15 words)",
  "robot_emotion": "happy | very_happy | sad | angry | surprised | neutral",
  "correction": "correct form if wrong, null if correct"
}}

robot_emotion rules:
- very_happy: score >= 90 AND time < 5s
- happy: score >= 70
- neutral: score >= 50
- sad: score < 50
- angry: score == 0
- surprised: score >= 95

Be lenient with minor spelling. Return ONLY JSON, no markdown."""

    raw = call_groq(prompt)
    raw = raw.replace("```json", "").replace("```", "").strip()
    result = json.loads(raw)

    xp = 0
    if result["is_correct"] or result.get("partial_credit"):
        xp += settings.XP_CORRECT_ANSWER
        if response_time_seconds < 5:
            xp += settings.XP_FAST_ANSWER_BONUS
    result["xp_earned"] = xp
    return result

async def generate_robot_message(emotion: str, context: str, language: str) -> str:
    prompt = f"""You are LinguaBot, a competitive robot language tutor.
Emotion: {emotion}, Context: {context}, Language: {language}
Write a SHORT message (max 10 words). Return ONLY the message, nothing else."""
    return call_groq(prompt)

async def generate_session_report(session_data: dict) -> dict:
    prompt = f"""Generate a post-game report for a language learning session.

Language: {session_data['language']}, Level: {session_data['level']}
Correct: {session_data['correct']} / {session_data['total']}
XP earned: {session_data['xp']}

Return ONLY valid JSON:
{{
  "summary": "2-sentence summary",
  "strengths": ["up to 2 strengths"],
  "improvements": ["up to 2 improvements"],
  "next_focus": "One suggestion",
  "motivational_quote": "Short robot message"
}}
Return ONLY JSON, no markdown."""

    raw = call_groq(prompt)
    raw = raw.replace("```json", "").replace("```", "").strip()
    return json.loads(raw)