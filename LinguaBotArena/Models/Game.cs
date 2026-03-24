namespace LinguaBotArena.Models;

public class Challenge
{
    public string Type { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
}

public class Evaluation
{
    public bool IsCorrect { get; set; }
    public bool PartialCredit { get; set; }
    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public string RobotEmotion { get; set; } = "neutral";
    public string? Correction { get; set; }
    public int XpEarned { get; set; }
}

public class XpBreakdown
{
    public int Correct { get; set; }
    public int SpeedBonus { get; set; }
    public int StreakBonus { get; set; }
    public int Total { get; set; }
}

public class LevelUpInfo
{
    public bool LeveledUp { get; set; }
    public int OldLevel { get; set; }
    public int NewLevel { get; set; }
    public string? NewRank { get; set; }
    public bool RankChanged { get; set; }
    public int TotalXp { get; set; }
    public int XpToNext { get; set; }
}

public class SessionReport
{
    public string Summary { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public string NextFocus { get; set; } = string.Empty;
    public string MotivationalQuote { get; set; } = string.Empty;
}

public class GameSession
{
    public int SessionId { get; set; }
    public int Level { get; set; }
    public string Language { get; set; } = string.Empty;
    public Challenge? CurrentChallenge { get; set; }
    public int Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public int XpEarned { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.Now;
}

public class ArenaRoom
{
    public string RoomCode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int TotalRounds { get; set; }
    public string Status { get; set; } = "waiting";
}

public class LeaderboardEntry
{
    public int Position { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Rank { get; set; } = string.Empty;
    public int Xp { get; set; }
    public int ArenaWins { get; set; }
    public string AvatarColor { get; set; } = "#00FF88";
    public double WinRate { get; set; }
}