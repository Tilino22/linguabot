using LinguaBotArena.Services;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Diagnostics;

namespace LinguaBotArena.Views;

public partial class ArenaGamePage : ContentPage
{
    // Datos transferidos desde ArenaLobbyPage
    public static string PendingRoomCode = "";
    public static JObject? PendingFirstRound = null;
    public static ClientWebSocket? PendingWebSocket = null;
    public static CancellationTokenSource? PendingWsCts = null;

    private ClientWebSocket? _ws;
    private CancellationTokenSource _wsCts = new();
    private string _roomCode = "";
    private Stopwatch _stopwatch = new();
    private CancellationTokenSource _timerCts = new();
    private bool _answered = false;
    private int _myScore = 0;
    private int _opponentScore = 0;
    private string _opponentName = "Oponente";

    public ArenaGamePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _ws = PendingWebSocket;
        _wsCts = PendingWsCts ?? new();
        _roomCode = PendingRoomCode;

        var user = SessionService.Instance.CurrentUser;
        Player1Label.Text = user?.Username ?? "Tú";

        if (PendingFirstRound != null)
        {
            _ = Task.Run(() => ListenWebSocket());
            MainThread.BeginInvokeOnMainThread(() => HandleRoundStart(PendingFirstRound));
            PendingFirstRound = null;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timerCts.Cancel();
        _wsCts.Cancel();
        _ws?.Dispose();
    }

    private async Task ListenWebSocket()
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

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    switch (evt)
                    {
                        case "round_start":    HandleRoundStart(data); break;
                        case "round_result":   HandleRoundResult(data); break;
                        case "answer_received": StatusLabel.Text = "⏳ Esperando oponente..."; break;
                        case "countdown":      HandleCountdown(data); break;
                        case "game_over":      HandleGameOver(data); break;
                        case "player_disconnected":
                            StatusLabel.Text = "⚠️ Oponente desconectado";
                            RobotEmoji.Text = "😢";
                            break;
                    }
                });
            }
        }
        catch (Exception) { }
    }

    private void HandleCountdown(JObject data)
    {
        var count = data["count"]?.ToObject<int>() ?? 3;
        StatusLabel.Text = $"⚔️ Iniciando en {count}...";
        RobotEmoji.Text = count switch { 3 => "😤", 2 => "😠", _ => "🔥" };
    }

    private void HandleRoundStart(JObject data)
    {
        _answered = false;
        var round = data["round"]?.ToObject<int>() ?? 1;
        var total = data["total_rounds"]?.ToObject<int>() ?? 5;
        var challenge = data["challenge"] as JObject;
        var timeLimit = data["time_limit"]?.ToObject<int>() ?? 30;

        RoundLabel.Text = $"Ronda {round}/{total}";
        StatusLabel.Text = "¡Responde rápido!";
        RobotEmoji.Text = "🤖";

        if (challenge != null)
        {
            var type = challenge["type"]?.ToString() ?? "";
            ChallengeType.Text = type.ToUpper().Replace("_", " ");
            ChallengeQuestion.Text = challenge["question"]?.ToString() ?? "";
            UpdateInstructions(type);
        }

        ChallengeBox.IsVisible = true;
        AnswerBox.IsVisible = true;
        FeedbackBox.IsVisible = false;
        AnswerEntry.Text = "";
        ActionButton.IsEnabled = true;
        ActionButton.Text = "ENVIAR RESPUESTA";

        StartTimer(timeLimit);
        AnswerEntry.Focus();
    }

    private void HandleRoundResult(JObject data)
    {
        _timerCts.Cancel();
        var results = data["results"] as JObject;
        var correctAnswer = data["correct_answer"]?.ToString() ?? "";
        var round = data["round"]?.ToObject<int>() ?? 1;

        CorrectAnswerLabel.Text = $"✅ Respuesta: {correctAnswer}";
        FeedbackTitle.Text = $"Resultado Ronda {round}";
        ResultsStack.Children.Clear();

        var myId = SessionService.Instance.CurrentUser?.Id.ToString();

        if (results != null)
        {
            foreach (var kv in results)
            {
                var playerResult = kv.Value as JObject;
                var username = playerResult?["username"]?.ToString() ?? "?";
                var evaluation = playerResult?["evaluation"] as JObject;
                var totalScore = playerResult?["total_score"]?.ToObject<int>() ?? 0;
                var isCorrect = evaluation?["is_correct"]?.ToObject<bool>() ?? false;
                var score = evaluation?["score"]?.ToObject<int>() ?? 0;

                if (kv.Key == myId)
                {
                    _myScore = totalScore;
                    Score1Label.Text = $"{totalScore} pts";
                }
                else
                {
                    _opponentName = username;
                    _opponentScore = totalScore;
                    Score2Label.Text = $"{totalScore} pts";
                    Player2Label.Text = username;
                }

                var row = new Border
                {
                    BackgroundColor = isCorrect ? Color.FromArgb("#0D1F0D") : Color.FromArgb("#1F0D0D"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(12, 8)
                };
                row.Content = new Label
                {
                    Text = $"{(isCorrect ? "✅" : "❌")} {username}: {score} pts  (total: {totalScore})",
                    TextColor = Colors.White,
                    FontSize = 14
                };
                ResultsStack.Children.Add(row);
            }
        }

        ChallengeBox.IsVisible = false;
        AnswerBox.IsVisible = false;
        FeedbackBox.IsVisible = true;
        ActionButton.IsEnabled = false;
        ActionButton.Text = "Siguiente ronda...";
        RobotEmoji.Text = _myScore >= _opponentScore ? "😄" : "😢";
    }

    private async void HandleGameOver(JObject data)
    {
        _timerCts.Cancel();
        var winnerUsername = data["winner_username"]?.ToString();
        var myName = SessionService.Instance.CurrentUser?.Username;
        var iWon = winnerUsername == myName;

        RobotEmoji.Text = iWon ? "🏆" : "😢";
        StatusLabel.Text = iWon ? "¡GANASTE! 🎉" : $"Ganó {winnerUsername}";
        FeedbackTitle.Text = "🏆 Fin del juego";

        ChallengeBox.IsVisible = false;
        AnswerBox.IsVisible = false;
        FeedbackBox.IsVisible = true;
        ActionButton.IsEnabled = true;
        ActionButton.Text = "VOLVER AL MENÚ";

        ResultsStack.Children.Clear();
        var finalScores = data["final_scores"] as JObject;
        if (finalScores != null)
        {
            foreach (var kv in finalScores)
            {
                ResultsStack.Children.Add(new Label
                {
                    Text = $"Jugador {kv.Key}: {kv.Value} pts",
                    TextColor = Colors.White,
                    FontSize = 14
                });
            }
        }
    }

    private async void OnActionClicked(object sender, EventArgs e)
    {
        if (ActionButton.Text == "VOLVER AL MENÚ")
        {
            await Shell.Current.GoToAsync("//MainMenuPage");
            return;
        }

        if (_answered) return;
        _answered = true;
        _timerCts.Cancel();

        var answer = AnswerEntry.Text?.Trim() ?? "";
        var responseTime = _stopwatch.Elapsed.TotalSeconds;

        ActionButton.IsEnabled = false;
        StatusLabel.Text = "⏳ Esperando oponente...";

        try
        {
            var msg = System.Text.Json.JsonSerializer.Serialize(new
            {
                @event = "submit_answer",
                answer = answer,
                response_time = responseTime
            });
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _ws!.SendAsync(bytes, WebSocketMessageType.Text, true, _wsCts.Token);
        }
        catch (Exception) { }
    }

    private void StartTimer(int seconds)
    {
        _timerCts = new CancellationTokenSource();
        _stopwatch.Restart();
        int total = seconds;

        Task.Run(async () =>
        {
            int remaining = total;
            while (remaining > 0 && !_timerCts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
                remaining--;
                double progress = (double)remaining / total;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TimerBar.Progress = progress;
                    TimerLabel.Text = $"{remaining}s";
                    TimerBar.ProgressColor = progress > 0.5
                        ? Color.FromArgb("#00FF88")
                        : progress > 0.25
                            ? Color.FromArgb("#FFE66D")
                            : Color.FromArgb("#FF6B6B");
                });
            }
        }, _timerCts.Token);
    }

    private void UpdateInstructions(string challengeType)
    {
        var (title, text) = challengeType switch
        {
            "translation"         => ("🌐 Traducción",          "Traduce la palabra o frase al idioma indicado"),
            "fill_blank"          => ("✏️ Completa el espacio",  "Escribe la palabra que falta donde dice ___"),
            "respond_in_language" => ("💬 Responde en el idioma","Lee la pregunta y responde en ese mismo idioma"),
            "word_definition"     => ("📖 Definición",           "Escribe el significado de la palabra mostrada"),
            "sentence_correction" => ("🔧 Corrección",           "Reescribe la oración corrigiendo el error"),
            _                     => ("📌 ¿Qué hacer?",          "Escribe tu respuesta y presiona ENVIAR")
        };
        InstructionTitle.Text = title;
        InstructionText.Text = text;
    }
}