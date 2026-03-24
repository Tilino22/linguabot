using LinguaBotArena.Views;

namespace LinguaBotArena;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
        Routing.RegisterRoute("MainMenuPage", typeof(MainMenuPage));
        Routing.RegisterRoute("TrainingPage", typeof(TrainingPage));
        Routing.RegisterRoute("ArenaLobbyPage", typeof(ArenaLobbyPage));
        Routing.RegisterRoute("LeaderboardPage", typeof(LeaderboardPage));
        Routing.RegisterRoute("ArenaGamePage", typeof(ArenaGamePage));
        Routing.RegisterRoute("VoiceTrainingPage", typeof(VoiceTrainingPage));
    }
}	