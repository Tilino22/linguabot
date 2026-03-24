using Newtonsoft.Json;
using LinguaBotArena.Models;
using System.Text;

namespace LinguaBotArena.Services;

public class ApiService
{
    private readonly HttpClient _client;
    private string _token = string.Empty;

    public ApiService()
    {
        string baseUrl = "https://linguabotv2-production.up.railway.app";
        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string GetBaseUrl()
    {
        return "linguabot-api-production.up.railway.app";
    }

    public void SetToken(string token)
    {
        _token = token;
    }

    public string GetToken() => _token;

    private async Task<T?> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    private async Task<T?> PostAsync<T>(string url, object body)
    {
        var json = JsonConvert.SerializeObject(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(responseJson);
    }

    // ── Auth ──────────────────────────────
    public async Task<TokenResponse?> Login(string username, string password)
    {
        var result = await PostAsync<TokenResponse>("auth/login", new
        {
            username,
            password
        });
        if (result != null)
            SetToken(result.AccessToken);
        return result;
    }

    public async Task<TokenResponse?> Register(string username, string email, string password, string avatarColor)
    {
        var result = await PostAsync<TokenResponse>("auth/register", new
        {
            username,
            email,
            password,
            avatar_color = avatarColor
        });
        if (result != null)
            SetToken(result.AccessToken);
        return result;
    }

    // ── Modo Local ────────────────────────
    public async Task<dynamic?> StartSession(string language, int? level = null)
    {
        if (string.IsNullOrEmpty(_token))
            _token = SessionService.Instance.CurrentUser?.AccessToken ?? "";

        if (string.IsNullOrEmpty(_token))
            _token = Preferences.Get("access_token", "");

        return await PostAsync<dynamic>("local/session/start", new
        {
            language,
            level,
            token = _token
        });
    }

    public async Task<dynamic?> SubmitAnswer(
        int sessionId, string challengeType, string question,
        string correctAnswer, string playerAnswer, double responseTime)
    {
        if (string.IsNullOrEmpty(_token))
            _token = Preferences.Get("access_token", "");

        return await PostAsync<dynamic>("local/answer", new
        {
            session_id = sessionId,
            challenge_type = challengeType,
            question,
            correct_answer = correctAnswer,
            player_answer = playerAnswer,
            response_time_seconds = responseTime,
            token = _token
        });
    }

    public async Task<dynamic?> EndSession(int sessionId)
    {
        if (string.IsNullOrEmpty(_token))
            _token = Preferences.Get("access_token", "");

        return await PostAsync<dynamic>("local/session/end", new
        {
            session_id = sessionId,
            token = _token
        });
    }

    // ── Arena ─────────────────────────────
    public async Task<dynamic?> CreateRoom(string language, int totalRounds = 5, string mode = "classic")
    {
        return await PostAsync<dynamic>($"arena/room/create?token={_token}", new
        {
            language,
            total_rounds = totalRounds,
            mode
        });
    }

    public async Task<dynamic?> GetRoom(string roomCode)
    {
        return await GetAsync<dynamic>($"arena/room/{roomCode}");
    }

    // ── Leaderboard ───────────────────────
    public async Task<dynamic?> GetLeaderboard(int limit = 10)
    {
        return await GetAsync<dynamic>($"players/leaderboard?limit={limit}");
    }

    public async Task<dynamic?> GetMyProfile()
    {
        return await GetAsync<dynamic>($"players/me?token={_token}");
    }
}