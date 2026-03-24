from app.core.config import settings


def xp_for_level(level: int) -> int:
    """XP total necesario para alcanzar este nivel."""
    return int(100 * (level ** 1.5))


def get_rank(level: int) -> str:
    if level <= 5:   return "Rookie"
    if level <= 15:  return "Apprentice"
    if level <= 30:  return "Fluent"
    if level <= 50:  return "Master"
    return "Legend"


def calculate_level_from_xp(total_xp: int) -> int:
    level = 1
    while xp_for_level(level + 1) <= total_xp:
        level += 1
    return level


def calculate_xp_reward(
    is_correct: bool,
    partial_credit: bool,
    response_time: float,
    current_streak: int,
    mode: str
) -> dict:
    breakdown = {}
    total = 0

    if is_correct:
        breakdown["correct"] = settings.XP_CORRECT_ANSWER
        total += settings.XP_CORRECT_ANSWER
    elif partial_credit:
        partial = settings.XP_CORRECT_ANSWER // 2
        breakdown["partial"] = partial
        total += partial

    # Bonus por velocidad
    if (is_correct or partial_credit) and response_time < 5:
        breakdown["speed_bonus"] = settings.XP_FAST_ANSWER_BONUS
        total += settings.XP_FAST_ANSWER_BONUS

    # Bonus por racha de 5
    if current_streak > 0 and current_streak % 5 == 0:
        breakdown["streak_bonus"] = settings.XP_STREAK_BONUS
        total += settings.XP_STREAK_BONUS

    breakdown["total"] = total
    return breakdown


def check_level_up(current_xp: int, xp_gained: int, current_level: int) -> dict:
    new_total_xp = current_xp + xp_gained
    new_level = calculate_level_from_xp(new_total_xp)
    leveled_up = new_level > current_level

    return {
        "leveled_up": leveled_up,
        "old_level": current_level,
        "new_level": new_level,
        "new_rank": get_rank(new_level) if leveled_up else None,
        "rank_changed": get_rank(new_level) != get_rank(current_level) if leveled_up else False,
        "total_xp": new_total_xp,
        "xp_to_next": xp_for_level(new_level + 1) - new_total_xp,
    }