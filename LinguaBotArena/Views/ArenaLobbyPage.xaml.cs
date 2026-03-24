using LinguaBotArena.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;

namespace LinguaBotArena.Views;

public partial class ArenaLobbyPage : ContentPage
{
    private readonly ApiService _api = SessionService.Instance.Api;
    private string _selectedLanguage = "english";
    private string _selectedMode = "classic";
    private string _currentRoomCode = "";
    private bool _isHost = false;
    private ClientWebSocket? _ws;
    private CancellationTokenSource _wsCts = new();
    private bool _wsTransferred = false;

    public ArenaLobbyPage()
    {
        InitializeComponent();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_wsTransferred)
        {
            _wsCts.Cancel();
            _ws?.Dispose();
        }
    }

    private void OnCreateLangSelected(object sender, TappedEventArgs e)
    {
        _selectedLanguage = e.Parameter?.ToString() ?? "english";
        CreateLangEn.Stroke = new SolidColorBrush(Color.FromArgb("#333355")); CreateLangEn.StrokeThickness = 1;
        CreateLangFr.Stroke = new SolidColorBrush(Color.FromArgb("#333355")); CreateLangFr.StrokeThickness = 1;
        CreateLangDe.Stroke = new SolidColorBrush(Color.FromArgb("#333355")); CreateLangDe.StrokeThickness = 1;

        var selected = _selectedLanguage switch
        {
            "english" => CreateLangEn,
            "french"  => CreateLangFr,
            "german"  => CreateLangDe,
            _         => CreateLangEn
        };
        selected.Stroke = new SolidColorBrush(Color.FromArgb("#00FF88"));
        selected.StrokeThickness = 2;
    }

    private void OnModeSelected(object sender, TappedEventArgs e)
    {
        _selectedMode = e.Parameter?.ToString() ?? "classic";
        ModeClassic.Stroke = new SolidColorBrush(Color.FromArgb("#333355")); ModeClassic.StrokeThickness = 1;
        ModeBattleRoyale.Stroke = new SolidColorBrush(Color.FromArgb("#333355")); ModeBattleRoyale.StrokeThickness = 1;

        var selected = _selectedMode == "battle_royale" ? ModeBattleRoyale : ModeClassic;
        selected.Stroke = new SolidColorBrush(Color.FromArgb("#FFE66D"));
        selected.StrokeThickness = 2;
    }

    private async void OnCreateRoomClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        RobotEmoji.Text = "🤔";
        StatusLabel.Text = "Creando sala...";

        try
        {
            var result = await _api.CreateRoom(_selectedLanguage, 10, _selectedMode);
            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var room = JObject.Parse(json);
                _currentRoomCode = room["room_code"]?.ToString() ?? "";
                _isHost = true;
                ShowActiveRoom(_currentRoomCode, _selectedLanguage, _selectedMode);
                RobotEmoji.Text = "😎";
                StatusLabel.Text = "¡Sala creada! Comparte el código";
                await ConnectWebSocket(_currentRoomCode);
            }
        }
        catch (Exception)
        {
            ErrorLabel.Text = "Error creando la sala";
            ErrorLabel.IsVisible = true;
            RobotEmoji.Text = "😢";
        }
    }

    private async void OnJoinRoomClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var code = RoomCodeEntry.Text?.Trim().ToUpper() ?? "";

        if (string.IsNullOrEmpty(code) || code.Length < 4)
        {
            ErrorLabel.Text = "Ingresa un código válido";
            ErrorLabel.IsVisible = true;
            return;
        }

        RobotEmoji.Text = "🔍";
        StatusLabel.Text = "Buscando sala...";

        try
        {
            var result = await _api.GetRoom(code);
            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var room = JObject.Parse(json);
                _currentRoomCode = code;
                _isHost = false;
                var lang = room["language"]?.ToString() ?? "english";
                var mode = room["mode"]?.ToString() ?? "classic";
                ShowActiveRoom(code, lang, mode);
                RobotEmoji.Text = "🤩";
                StatusLabel.Text = "¡Sala encontrada! Conectando...";
                await ConnectWebSocket(code);
            }
            else
            {
                ErrorLabel.Text = "Sala no encontrada";
                ErrorLabel.IsVisible = true;
                RobotEmoji.Text = "😕";
            }
        }
        catch (Exception)
        {
            ErrorLabel.Text = "Error buscando la sala";
            ErrorLabel.IsVisible = true;
        }
    }

    private async void OnStartGameClicked(object sender, EventArgs e)
    {
        if (_ws?.State != WebSocketState.Open) return;

        var msg = System.Text.Json.JsonSerializer.Serialize(new { @event = "start_game" });
        var bytes = Encoding.UTF8.GetBytes(msg);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _wsCts.Token);

        StartGameButton.IsEnabled = false;
        StartGameButton.Text = "⏳ Iniciando...";
    }

    private async Task ConnectWebSocket(string code)
    {
        var user = SessionService.Instance.CurrentUser;
        if (user == null) return;

        _ws = new ClientWebSocket();
        _wsCts = new CancellationTokenSource();
        _wsTransferred = false;

        try
        {
            var host = _api.GetBaseUrl();
            var uri = new Uri($"ws://{host}:8000/arena/ws/{code}/{user.Id}/{user.Username}");
            await _ws.ConnectAsync(uri, _wsCts.Token);
            StatusLabel.Text = "⚡ Conectado — esperando jugadores...";
            RobotEmoji.Text = "👀";
            _ = Task.Run(() => ListenWebSocket(code));
        }
        catch (Exception)
        {
            ErrorLabel.Text = "Error conectando al WebSocket";
            ErrorLabel.IsVisible = true;
        }
    }

    private async Task ListenWebSocket(string code)
    {
        var buffer = new byte[4096];

        try
        {
            while (_ws?.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(buffer, _wsCts.Token);
                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var data = JObject.Parse(msg);
                var evt = data["event"]?.ToString();

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    switch (evt)
                    {
                        case "player_joined":
                            var players = data["players"]?.ToObject<List<string>>() ?? new();
                            var count = data["player_count"]?.ToObject<int>() ?? players.Count;
                            UpdatePlayersList(players);
                            PlayerCountLabel.Text = $"👥 {count} jugador(es) en sala";
                            StatusLabel.Text = $"👥 {count} jugador(es) conectados";
                            RobotEmoji.Text = "👀";
                            if (_isHost && count >= 2)
                            {
                                StartGameButton.IsVisible = true;
                                StartGameButton.IsEnabled = true;
                                StartGameButton.Text = "🎮 INICIAR BATALLA";
                            }
                            break;

                        case "player_disconnected":
                            var countAfter = data["player_count"]?.ToObject<int>() ?? 0;
                            PlayerCountLabel.Text = $"👥 {countAfter} jugador(es) en sala";
                            if (_isHost && countAfter < 2)
                                StartGameButton.IsVisible = false;
                            break;

                        case "countdown":
                            var count2 = data["count"]?.ToObject<int>() ?? 3;
                            StatusLabel.Text = $"⚔️ Iniciando en {count2}...";
                            RobotEmoji.Text = count2 switch { 3 => "😤", 2 => "😠", _ => "🔥" };
                            break;

                        case "game_starting":
                            StatusLabel.Text = "🔥 ¡La batalla comienza!";
                            RobotEmoji.Text = "🔥";
                            break;

                        case "round_start":
                            await NavigateToGame(data, code);
                            break;

                        case "error":
                            ErrorLabel.Text = data["message"]?.ToString();
                            ErrorLabel.IsVisible = true;
                            break;
                    }
                });
            }
        }
        catch (Exception) { }
    }

    private async Task NavigateToGame(JObject firstRound, string code)
    {
        _wsTransferred = true;
        ArenaGamePage.PendingRoomCode = code;
        ArenaGamePage.PendingFirstRound = firstRound;
        ArenaGamePage.PendingWebSocket = _ws;
        ArenaGamePage.PendingWsCts = _wsCts;
        _ws = null;
        await Shell.Current.GoToAsync("ArenaGamePage");
    }


    private void ShowActiveRoom(string code, string language, string mode)
    {
        CreateRoomSection.IsVisible = false;
        JoinRoomSection.IsVisible = false;

        ActiveRoomPanel.IsVisible = true;
        RoomCodeLabel.Text = $"Código: {code}";
        RoomLangLabel.Text = language switch
        {
            "english" => "🇺🇸 Inglés",
            "french"  => "🇫🇷 Francés",
            "german"  => "🇩🇪 Alemán",
            _ => language
        };
        RoomModeLabel.Text = mode == "battle_royale"
            ? "👑 Battle Royale — último en pie gana"
            : "⚔️ Modo Clásico";
        PlayersStack.Children.Clear();
        var username = SessionService.Instance.CurrentUser?.Username ?? "Tú";
        AddPlayerToList(username, _isHost ? "👑 Host" : "Jugador");
        StartGameButton.IsVisible = false;
        WaitHostLabel.IsVisible = !_isHost;
        PlayerCountLabel.Text = "👥 1 jugador en sala";
    }

    private void UpdatePlayersList(List<string> players)
    {
        PlayersStack.Children.Clear();
        for (int i = 0; i < players.Count; i++)
            AddPlayerToList(players[i], i == 0 ? "👑 Host" : "⚔️ Jugador");
        WaitingLabel.IsVisible = players.Count < 2;
    }

    private void AddPlayerToList(string username, string role)
    {
        var container = new Border
        {
            BackgroundColor = Color.FromArgb("#1E1E3A"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12, 8)
        };
        container.Content = new Label
        {
            Text = $"🤖 {username}  •  {role}",
            TextColor = Colors.White,
            FontSize = 14
        };
        PlayersStack.Children.Add(container);
    }


    private async void OnBackClicked(object sender, EventArgs e)
    {
        _wsCts.Cancel();
        _ws?.Dispose();

        CreateRoomSection.IsVisible = true;
        JoinRoomSection.IsVisible = true;
        ActiveRoomPanel.IsVisible = false;

        await Shell.Current.GoToAsync("..");
    }
}