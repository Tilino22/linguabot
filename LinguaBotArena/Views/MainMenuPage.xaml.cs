using LinguaBotArena.Services;

namespace LinguaBotArena.Views;

public partial class MainMenuPage : ContentPage
{
    public MainMenuPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUserData();
        AnimateRobot();
    }

    private void LoadUserData()
    {
        var user = SessionService.Instance.CurrentUser;
        if (user == null) return;

        WelcomeLabel.Text = $"Hola, {user.Username}!";
        RankLabel.Text = $"{user.Rank} • Nivel {user.Level}";
        XpLabel.Text = $"{user.Xp} XP";

        double progress = (user.Xp % 100) / 100.0;
        XpBar.Progress = progress;
    }

    private async void AnimateRobot()
    {
        string[] emotions = { "🤖", "😄", "💪", "🤖" };
        string[] messages = {
            "¡Listo para aprender!",
            "¡Hoy superamos el record!",
            "¡El Arena te espera!",
            "¿Qué idioma practicamos?"
        };

        for (int i = 0; i < emotions.Length; i++)
        {
            RobotEmoji.Text = emotions[i];
            RobotMessage.Text = messages[i];
            await Task.Delay(2000);
        }
    }

    private async void OnTrainingTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("TrainingPage");
    }

    private async void OnVoiceTrainingClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("VoiceTrainingPage");
    }

    private async void OnArenaTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ArenaLobbyPage");
    }

    private async void OnLeaderboardTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("LeaderboardPage");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Cerrar Sesión",
            "¿Seguro que quieres salir?",
            "Sí", "No");

        if (confirm)
        {
            SessionService.Instance.Logout();
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}