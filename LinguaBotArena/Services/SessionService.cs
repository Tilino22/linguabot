using LinguaBotArena.Models;

namespace LinguaBotArena.Services;

public class SessionService
{
    private static SessionService? _instance;
    public static SessionService Instance => _instance ??= new SessionService();

    // Usuario actual
    public User? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null;

    // Sesión de juego actual
    public GameSession? CurrentSession { get; private set; }

    // ── Auth ──────────────────────────────
    public void SetUser(TokenResponse token)
    {
        CurrentUser = new User
        {
            Id = token.UserId,
            Username = token.Username,
            Level = token.Level,
            Rank = token.Rank,
            Xp = token.Xp,
            AccessToken = token.AccessToken,
        };
    }

    public void Logout()
    {
        CurrentUser = null;
        CurrentSession = null;
    }

    // ── Sesión de juego ───────────────────
    public void StartSession(int sessionId, string language, int level)
    {
        CurrentSession = new GameSession
        {
            SessionId = sessionId,
            Language = language,
            Level = level,
            StartedAt = DateTime.Now,
        };
    }

    public void UpdateSession(bool isCorrect, int xpEarned, int score)
    {
        if (CurrentSession == null) return;

        CurrentSession.Score += score;
        CurrentSession.XpEarned += xpEarned;

        if (isCorrect)
        {
            CurrentSession.CorrectAnswers++;
            CurrentSession.CurrentStreak++;
        }
        else
        {
            CurrentSession.WrongAnswers++;
            CurrentSession.CurrentStreak = 0;
        }
    }

    public void UpdateUserXp(int newXp, int newLevel, string newRank)
    {
        if (CurrentUser == null) return;
        CurrentUser.Xp = newXp;
        CurrentUser.Level = newLevel;
        CurrentUser.Rank = newRank;
    }

    public void EndSession()
    {
        CurrentSession = null;
    }

    // ── Stats rápidos ─────────────────────
    public double GetCurrentAccuracy()
    {
        if (CurrentSession == null) return 0;
        int total = CurrentSession.CorrectAnswers + CurrentSession.WrongAnswers;
        if (total == 0) return 0;
        return Math.Round((double)CurrentSession.CorrectAnswers / total * 100, 1);
    }

    public TimeSpan GetSessionDuration()
    {
        if (CurrentSession == null) return TimeSpan.Zero;
        return DateTime.Now - CurrentSession.StartedAt;
    }

    // API Service singleton
    public ApiService Api { get; } = new ApiService();
}