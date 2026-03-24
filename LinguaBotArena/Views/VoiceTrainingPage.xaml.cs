using LinguaBotArena.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LinguaBotArena.Views;

public partial class VoiceTrainingPage : ContentPage
{
    private readonly ApiService _api = SessionService.Instance.Api;
    private string _selectedLanguage = "";
    private bool _languageConfirmed = false;
    private int _sessionId = 0;
    private string _currentChallengeType = "";
    private string _currentQuestion = "";
    private string _currentCorrectAnswer = "";
    private bool _isListening = false;
    private bool _isPlaying = false;
    private CancellationTokenSource _listenCts = new();
    private RobotDrawable _robotDrawable = new();
    private IDispatcherTimer? _robotTimer;

    private static readonly Dictionary<string, (string emoji, string name, string description, string tip)> LangInfo = new()
    {
        ["english"] = ("🇺🇸", "Inglés",  "El idioma más hablado del mundo. Practica pronunciación y fluidez.", "Habla claro y a velocidad normal. El robot te escuchará."),
        ["french"]  = ("🇫🇷", "Francés", "La lengua del amor. Pronunciación elegante y melodiosa.", "Las consonantes finales suelen ser mudas. ¡Escucha y repite!"),
        ["german"]  = ("🇩🇪", "Alemán",  "Idioma de la precisión. Cada palabra se pronuncia como se escribe.", "Pronuncia cada letra claramente. El alemán es muy fonético."),
    };

    public VoiceTrainingPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var user = SessionService.Instance.CurrentUser;
        if (user != null)
            LevelLabel.Text = $"Nivel {user.Level}";
        LoadRobot3D();
    }

    private void LoadRobot3D()
    {
        RobotView.Drawable = _robotDrawable;
        _robotTimer = Application.Current!.Dispatcher.CreateTimer();
        _robotTimer.Interval = TimeSpan.FromMilliseconds(33);
        _robotTimer.Tick += (s, e) =>
        {
            _robotDrawable.T += 0.05f;
            _robotDrawable.Talking = _robotDrawable.Emotion == "talking";
            RobotView.Invalidate();
        };
        _robotTimer.Start();
    }

    private void SetRobotEmotion(string emotion)
    {
        _robotDrawable.Emotion = emotion;
        _robotDrawable.Talking = emotion == "talking";
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
        SetRobotEmotion("talking");

        // Modal de confirmación
        bool confirmed = await DisplayAlert(
            $"{info.emoji} ¿Listo para practicar {info.name}?",
            $"{info.description}\n\n🎤 {info.tip}",
            "¡Vamos! 🚀",
            "Cancelar"
        );

        if (!confirmed)
        {
            SetRobotEmotion("neutral");
            return;
        }

        _selectedLanguage = lang;
        _languageConfirmed = true;

        // Reset y seleccionar
        BtnEnglish.BackgroundColor = Color.FromArgb("#1A1A2E");
        BtnFrench.BackgroundColor  = Color.FromArgb("#1A1A2E");
        BtnGerman.BackgroundColor  = Color.FromArgb("#1A1A2E");
        btn.BackgroundColor = Color.FromArgb("#00FF88");
        btn.TextColor = Color.FromArgb("#0A0A1A");

        // Bloquear otros botones
        LockLanguageButtons(lang);

        MicButton.IsEnabled = true;
        MicBox.IsVisible = true;
        MicStatusLabel.Text = "Presiona para comenzar";
        SetRobotEmotion("neutral");
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

    private async void OnMicButtonClicked(object sender, EventArgs e)
    {
        if (!_languageConfirmed) return;
        if (!_isPlaying)
            await StartVoiceSession();
        else if (!_isListening)
            await StartListening();
        else
            await StopListening();
    }

    private async Task StartVoiceSession()
    {
        MicButton.IsEnabled = false;
        MicButton.Text = "Cargando...";
        SetRobotEmotion("talking");
        try
        {
            var user = SessionService.Instance.CurrentUser;
            var result = await _api.StartSession(_selectedLanguage, user?.Level);
            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var data = JObject.Parse(json);
                _sessionId = data["session_id"]?.ToObject<int>() ?? 0;
                ShowChallenge(data["challenge"] as JObject);
                _isPlaying = true;
                await SpeakQuestion();
            }
        }
        catch (Exception)
        {
            MicStatusLabel.Text = "Error conectando al servidor";
        }
        finally
        {
            MicButton.IsEnabled = true;
            MicButton.Text = "HABLAR";
        }
    }

    private void ShowChallenge(JObject? challenge)
    {
        if (challenge == null) return;
        _currentChallengeType = challenge["type"]?.ToString() ?? "";
        _currentQuestion = challenge["question"]?.ToString() ?? "";
        _currentCorrectAnswer = challenge["correct_answer"]?.ToString() ?? "";
        QuestionTypeLabel.Text = _currentChallengeType.ToUpper().Replace("_", " ");
        QuestionLabel.Text = _currentQuestion;

        var hint = challenge["hint"]?.ToString();
        if (!string.IsNullOrEmpty(hint)) { HintLabel.Text = $"💡 {hint}"; HintLabel.IsVisible = true; }
        else HintLabel.IsVisible = false;

        QuestionBox.IsVisible = true;
        FeedbackBox.IsVisible = false;
        MicBox.IsVisible = true;
        TranscriptLabel.IsVisible = false;
        MicStatusLabel.Text = "Presiona HABLAR y responde";

        InstructionsBox.IsVisible = true;
        InstructionsLabel.Text = _currentChallengeType switch
        {
            "translation"         => "Escucha la frase y dila en voz alta en el idioma indicado.",
            "fill_blank"          => "Di en voz alta solo la palabra que falta en el espacio.",
            "sentence_correction" => "Escucha la oración con el error y dila correctamente.",
            "word_definition"     => "Di en voz alta la definición de la palabra.",
            "conjugation"         => "Conjuga el verbo en voz alta.",
            "vocabulary"          => "Di en voz alta la traducción de la palabra.",
            _                     => "Escucha y responde hablando de forma clara."
        };
    }

    private async Task SpeakQuestion()
    {
        SetRobotEmotion("talking");
        var locale = _selectedLanguage switch
        {
            "english" => "en-US",
            "french"  => "fr-FR",
            "german"  => "de-DE",
            _ => "en-US"
        };
        await TextToSpeech.SpeakAsync(_currentQuestion, new SpeechOptions
        {
            Locale = await GetLocale(locale),
            Pitch = 1.2f,
            Volume = 1.0f
        });
        SetRobotEmotion("neutral");
    }

    private async Task<Locale?> GetLocale(string localeCode)
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        return locales.FirstOrDefault(l => l.Language.StartsWith(localeCode.Split('-')[0]));
    }

    private async Task StartListening()
    {
        _isListening = true;
        _listenCts = new CancellationTokenSource();
        MicButton.Text = "DETENER";
        MicButton.BackgroundColor = Color.FromArgb("#FF4444");
        MicEmoji.Text = "Escuchando...";
        MicStatusLabel.Text = "Habla o escribe tu respuesta...";
        SetRobotEmotion("listening");

        try
        {
            var result = await DisplayPromptAsync(
                "Tu respuesta",
                _currentQuestion,
                "Enviar", "Cancelar",
                placeholder: "Habla usando el micrófono del teclado o escribe...",
                keyboard: Keyboard.Default
            );

            if (!string.IsNullOrEmpty(result))
            {
                TranscriptLabel.Text = $"\"{result}\"";
                TranscriptLabel.IsVisible = true;
                MicStatusLabel.Text = "Evaluando...";
                await SubmitVoiceAnswer(result);
            }
            else
            {
                MicStatusLabel.Text = "Presiona HABLAR para responder";
                SetRobotEmotion("neutral");
            }
        }
        catch (Exception)
        {
            MicStatusLabel.Text = "Error, intenta de nuevo";
        }
        finally
        {
            _isListening = false;
            MicButton.Text = "HABLAR";
            MicButton.BackgroundColor = Color.FromArgb("#00FF88");
            MicEmoji.Text = "Mic";
        }
    }

    private async Task StopListening()
    {
        _listenCts.Cancel();
        _isListening = false;
        MicButton.Text = "HABLAR";
        MicButton.BackgroundColor = Color.FromArgb("#00FF88");
        MicEmoji.Text = "Mic";
    }

    private async Task SubmitVoiceAnswer(string answer)
    {
        try
        {
            var result = await _api.SubmitAnswer(
                _sessionId, _currentChallengeType, _currentQuestion,
                _currentCorrectAnswer, answer, 5.0
            );
            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var data = JObject.Parse(json);
                await ShowFeedback(data);
                var nextChallenge = data["next_challenge"] as JObject;
                if (nextChallenge != null)
                {
                    await Task.Delay(3000);
                    ShowChallenge(nextChallenge);
                    await SpeakQuestion();
                }
            }
        }
        catch (Exception)
        {
            MicStatusLabel.Text = "Error evaluando respuesta";
        }
    }

    private async Task ShowFeedback(JObject data)
    {
        var evaluation = data["evaluation"] as JObject;
        var xpBreakdown = data["xp_breakdown"] as JObject;
        bool isCorrect = evaluation?["is_correct"]?.ToObject<bool>() ?? false;
        string feedback = evaluation?["feedback"]?.ToString() ?? "";
        int xp = xpBreakdown?["total"]?.ToObject<int>() ?? 0;
        int streak = data["current_streak"]?.ToObject<int>() ?? 0;

        SetRobotEmotion(isCorrect ? "very_happy" : "sad");
        if (isCorrect) { _robotDrawable.IsJumping = true; _robotDrawable.JumpT = 0; }
        else { _robotDrawable.IsShaking = true; _robotDrawable.ShakeT = 0; }

        FeedbackEmoji.Text = isCorrect ? "Correcto!" : "Incorrecto";
        FeedbackText.Text = feedback;
        XpText.Text = xp > 0 ? $"+{xp} XP" : "Sin XP";
        StreakLabel.Text = $"Racha: {streak}";

        var correction = evaluation?["correction"]?.ToString();
        if (!string.IsNullOrEmpty(correction) && correction != "null")
        {
            CorrectionText.Text = $"Correcto: {correction}";
            CorrectionText.IsVisible = true;
        }
        else CorrectionText.IsVisible = false;

        FeedbackBox.Stroke = isCorrect
            ? new SolidColorBrush(Color.FromArgb("#00FF88"))
            : new SolidColorBrush(Color.FromArgb("#FF6B6B"));
        FeedbackBox.IsVisible = true;

        SetRobotEmotion("talking");
        await TextToSpeech.SpeakAsync(feedback);
        SetRobotEmotion("neutral");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        _robotTimer?.Stop();
        if (_isPlaying && _sessionId > 0)
            await _api.EndSession(_sessionId);

        // Resetear estado al regresar
        _isPlaying = false;
        _sessionId = 0;
        UnlockLanguageButtons();
        QuestionBox.IsVisible = false;
        FeedbackBox.IsVisible = false;
        MicBox.IsVisible = false;
        InstructionsBox.IsVisible = false;
        MicButton.IsEnabled = false;
        MicButton.Text = "HABLAR";
        SetRobotEmotion("neutral");

        await Shell.Current.GoToAsync("..");
    }
}