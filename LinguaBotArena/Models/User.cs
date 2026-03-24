namespace LinguaBotArena.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#00FF88";
    public int Level { get; set; } = 1;
    public string Rank { get; set; } = "Rookie";
    public int Xp { get; set; } = 0;
    public string AccessToken { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#00FF88";
}

public class TokenResponse
{
    [Newtonsoft.Json.JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [Newtonsoft.Json.JsonProperty("token_type")]
    public string TokenType { get; set; } = "bearer";

    [Newtonsoft.Json.JsonProperty("user_id")]
    public int UserId { get; set; }

    [Newtonsoft.Json.JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [Newtonsoft.Json.JsonProperty("level")]
    public int Level { get; set; }

    [Newtonsoft.Json.JsonProperty("rank")]
    public string Rank { get; set; } = string.Empty;

    [Newtonsoft.Json.JsonProperty("xp")]
    public int Xp { get; set; }
}