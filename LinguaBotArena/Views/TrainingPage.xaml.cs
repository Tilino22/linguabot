using LinguaBotArena.Services;
using Newtonsoft.Json;
using System.Diagnostics;

namespace LinguaBotArena.Views;

public partial class TrainingPage : ContentPage
{
    private readonly ApiService _api = SessionService.Instance.Api;
    private string _selectedLanguage = "";
    private bool _languageConfirmed = false;
    private int _sessionId = 0;
    private string _currentChallengeType = "";
    private string _currentQuestion = "";
    private string _currentCorrectAnswer = "";
    private bool _isPlaying = false;
    private bool _waitingNext = false;
    private Stopwatch _stopwatch = new();
    private CancellationTokenSource _timerCts = new();

    private static readonly Dictionary<string, (string emoji, string name, string description, string tip)> LangInfo = new()
    {
        ["english"] = ("🇺🇸", "Inglés", "El idioma más hablado del mundo. Vocabulario, gramática y frases cotidianas.", "Habla claro y sin prisa. ¡Cada respuesta cuenta!"),
        ["french"]  = ("🇫🇷", "Francés", "La lengua del amor y la diplomacia. Pronunciación elegante y vocabulario rico.", "Presta atención a los artículos y el género de las palabras."),
        ["german"]  = ("🇩🇪", "Alemán", "Idioma de la precisión y la filosofía. Estructura de oraciones única.", "El orden de las palabras es clave en alemán. ¡Practica mucho!"),
    };

    public TrainingPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var user = SessionService.Instance.CurrentUser;
        if (user != null)
            LevelLabel.Text = $"Nivel {user.Level}";
    }

    private async void OnLanguageSelected(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        // Si ya está jugando, ignorar
        if (_isPlaying) return;

        var lang = btn.Text switch
        {
            var t when t.Contains("Inglés") => "english",
            var t when t.Contains("Francés") => "french",
            var t when t.Contains("Alemán") => "german",
            _ => "english"
        };

        var info = LangInfo[lang];

        // Mostrar modal de confirmación
        bool confirmed = await DisplayAlert(
            $"{info.emoji} ¿Listo para practicar {info.name}?",
            $"{info.description}\n\n💡 {info.tip}",
            "¡Vamos! 🚀",
            "Cancelar"
        );

        if (!confirmed) return;

        // Confirmar idioma y bloquear botones
        _selectedLanguage = lang;
        _languageConfirmed = true;

        // Reset colores
        BtnEnglish.BackgroundColor = Color.FromArgb("#1A1A2E");
        BtnFrench.BackgroundColor  = Color.FromArgb("#1A1A2E");
        BtnGerman.BackgroundColor  = Color.FromArgb("#1A1A2E");

        btn.BackgroundColor = Color.FromArgb("#00FF88");
        btn.TextColor = Color.FromArgb("#0A0A1A");

        // Deshabilitar los otros botones
        LockLanguageButtons(lang);

        RobotMessage.Text = $"¡Vamos a practicar {info.name}! {info.emoji}";
        RobotEmoji.Text = "💪";

        // Iniciar sesión automáticamente
        await StartSession();
    }

    private void LockLanguageButtons(string selectedLang)
    {
        BtnEnglish.IsEnabled = selectedLang == "english";
        BtnFrench.IsEnabled  = selectedLang == "french";
        BtnGerman.IsEnabled  = selectedLang == "german";
        BtnEnglish.Opacity   = selectedLang == "english" ? 1.0 : 0.4;
        BtnFrench.Opacity    = selectedLang == "french"  ? 1.0 : 0.4;
        BtnGerman.Opacity    = selectedLang == "german"  ? 1.0 : 0.4;
    }

    private void UnlockLanguageButtons()
    {
        BtnEnglish.IsEnabled = true;
        BtnFrench.IsEnabled  = true;
        BtnGerman.IsEnabled  = true;
        BtnEnglish.Opacity   = 1.0;
        BtnFrench.Opacity    = 1.0;
        BtnGerman.Opacity    = 1.0;
        BtnEnglish.BackgroundColor = Color.FromArgb("#1A1A2E");
        BtnFrench.BackgroundColor  = Color.FromArgb("#1A1A2E");
        BtnGerman.BackgroundColor  = Color.FromArgb("#1A1A2E");
        BtnEnglish.TextColor = Colors.White;
        BtnFrench.TextColor  = Colors.White;
        BtnGerman.TextColor  = Colors.White;
        _languageConfirmed = false;
        _selectedLanguage = "";
    }

    private async void OnActionClicked(object sender, EventArgs e)
    {
        if (!_languageConfirmed)
        {
            RobotMessage.Text = "Selecciona un idioma primero 👆";
            RobotEmoji.Text = "👆";
            return;
        }

        if (!_isPlaying)
            await StartSession();
        else if (_waitingNext)
            await LoadNextChallenge();
        else
            await SubmitAnswer();
    }

    private async Task StartSession()
    {
        ActionButton.IsEnabled = false;
        ActionButton.Text = "Cargando...";
        RobotEmoji.Text = "🤔";
        RobotMessage.Text = "Preparando tu desafío...";

        try
        {
            var user = SessionService.Instance.CurrentUser;
            var result = await _api.StartSession(_selectedLanguage, user?.Level);

            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var data = JsonConvert.DeserializeObject<dynamic>(json);

                _sessionId = (int)data["session_id"];
                SessionService.Instance.StartSession(_sessionId, _selectedLanguage, user?.Level ?? 1);

                ShowChallenge(data["challenge"]);
                _isPlaying = true;
                ActionButton.Text = "ENVIAR RESPUESTA";
            }
        }
        catch (Exception)
        {
            RobotMessage.Text = "Error conectando con el servidor 😞";
            RobotEmoji.Text = "😞";
        }
        finally
        {
            ActionButton.IsEnabled = true;
        }
    }

    private void ShowChallenge(dynamic challenge)
    {
        _currentChallengeType = (string)challenge["type"];
        _currentQuestion = (string)challenge["question"];
        _currentCorrectAnswer = (string)challenge["correct_answer"];

        ChallengeType.Text = _currentChallengeType.ToUpper().Replace("_", " ");
        UpdateInstructions(_currentChallengeType);
        ChallengeQuestion.Text = _currentQuestion;
        HintLabel.Text = $"💡 {challenge["hint"]}";
        HintLabel.IsVisible = false;

        ChallengeBox.IsVisible = true;
        AnswerBox.IsVisible = true;
        FeedbackBox.IsVisible = false;
        AnswerEntry.Text = "";
        AnswerEntry.Focus();

        _waitingNext = false;
        StartTimer();

        RobotEmoji.Text = "🤖";
        RobotMessage.Text = "¡Responde lo más rápido que puedas!";
    }

    private async Task SubmitAnswer()
    {
        if (string.IsNullOrWhiteSpace(AnswerEntry.Text))
        {
            HintLabel.Text = "⚠️ Escribe una respuesta primero";
            HintLabel.IsVisible = true;
            return;
        }

        _timerCts.Cancel();
        ActionButton.IsEnabled = false;
        ActionButton.Text = "Evaluando...";

        double responseTime = _stopwatch.Elapsed.TotalSeconds;

        try
        {
            var result = await _api.SubmitAnswer(
                _sessionId,
                _currentChallengeType,
                _currentQuestion,
                _currentCorrectAnswer,
                AnswerEntry.Text.Trim(),
                responseTime
            );

            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var data = JsonConvert.DeserializeObject<dynamic>(json);
                ShowFeedback(data);
                UpdateNextChallenge(data["next_challenge"]);
            }
        }
        catch (Exception)
        {
            RobotMessage.Text = "Error al evaluar 😞";
        }
        finally
        {
            ActionButton.IsEnabled = true;
            ActionButton.Text = "SIGUIENTE →";
            _waitingNext = true;
        }
    }

    private void ShowFeedback(dynamic data)
    {
        var evaluation = data["evaluation"];
        var xpBreakdown = data["xp_breakdown"];
        var levelUp = data["level_up"];

        bool isCorrect = (bool)evaluation["is_correct"];
        string emotion = (string)evaluation["robot_emotion"];
        string feedback = (string)evaluation["feedback"];
        int xp = (int)xpBreakdown["total"];
        int streak = (int)data["current_streak"];

        RobotEmoji.Text = emotion switch
        {
            "very_happy" => "🤩",
            "happy"      => "😄",
            "sad"        => "😢",
            "angry"      => "😠",
            "surprised"  => "🤯",
            _            => "🤖"
        };
        RobotMessage.Text = feedback;

        FeedbackEmoji.Text = isCorrect ? "✅" : "❌";
        FeedbackText.Text  = feedback;
        XpText.Text = xp > 0 ? $"+{xp} XP" : "Sin XP esta vez";

        var correction = evaluation["correction"];
        if (correction != null && correction.ToString() != "")
        {
            CorrectionText.Text = $"Correcto: {correction}";
            CorrectionText.IsVisible = true;
        }
        else CorrectionText.IsVisible = false;

        FeedbackBox.StrokeThickness = 2;
        FeedbackBox.Stroke = isCorrect ? Color.FromArgb("#00FF88") : Color.FromArgb("#FF6B6B");
        FeedbackBox.IsVisible = true;

        StreakLabel.Text = $"🔥 {streak}";
        SessionService.Instance.UpdateSession(isCorrect, xp, isCorrect ? 10 : 0);

        if ((bool)levelUp["leveled_up"])
        {
            var newLevel = (int)levelUp["new_level"];
            LevelLabel.Text = $"Nivel {newLevel}";
            RobotMessage.Text = $"🎉 ¡Subiste al nivel {newLevel}!";
            SessionService.Instance.UpdateUserXp(
                (int)levelUp["total_xp"],
                newLevel,
                (string)(levelUp["new_rank"] ?? "Rookie")
            );
        }
    }

    private dynamic? _nextChallenge = null;
    private void UpdateNextChallenge(dynamic challenge) => _nextChallenge = challenge;

    private async Task LoadNextChallenge()
    {
        if (_nextChallenge != null)
        {
            ShowChallenge(_nextChallenge);
            _nextChallenge = null;
            ActionButton.Text = "ENVIAR RESPUESTA";
            _waitingNext = false;
        }
    }

    private void StartTimer()
    {
        _timerCts = new CancellationTokenSource();
        _stopwatch.Restart();

        Task.Run(async () =>
        {
            int seconds = 30;
            while (seconds > 0 && !_timerCts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
                seconds--;
                double progress = seconds / 30.0;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TimerBar.Progress = progress;
                    TimerLabel.Text = $"{seconds}s";
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
            "fill_blank"          => ("✏️ Completa el espacio", "Escribe la palabra que falta donde dice ___"),
            "respond_in_language" => ("💬 Responde en el idioma","Lee la pregunta y responde en ese mismo idioma"),
            "word_definition"     => ("📖 Definición",           "Escribe el significado de la palabra mostrada"),
            "sentence_correction" => ("🔧 Corrección",           "Reescribe la oración corrigiendo el error"),
            _                     => ("📌 ¿Qué hacer?",          "Escribe tu respuesta y presiona RESPONDER")
        };
        InstructionTitle.Text = title;
        InstructionText.Text  = text;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        _timerCts.Cancel();
        if (_isPlaying && _sessionId > 0)
            await _api.EndSession(_sessionId);

        // Desbloquear idioma al regresar
        _isPlaying = false;
        _sessionId = 0;
        UnlockLanguageButtons();
        ChallengeBox.IsVisible = false;
        AnswerBox.IsVisible = false;
        FeedbackBox.IsVisible = false;
        ActionButton.Text = "COMENZAR";
        RobotEmoji.Text = "🤖";
        RobotMessage.Text = "¡Selecciona un idioma para comenzar!";

        await Shell.Current.GoToAsync("..");
    }
}