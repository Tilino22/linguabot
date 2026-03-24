using LinguaBotArena.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LinguaBotArena.Views;

public partial class LeaderboardPage : ContentPage
{
    private readonly ApiService _api = SessionService.Instance.Api;

    public LeaderboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLeaderboard();
    }

private async Task LoadLeaderboard()
{
    LoadingIndicator.IsVisible = true;
    LoadingIndicator.IsRunning = true;
    ErrorLabel.IsVisible = false;
    LeaderboardStack.Children.Clear();

    var me = SessionService.Instance.CurrentUser;
    if (me != null)
    {
        MyUsernameLabel.Text = me.Username;
        MyRankNameLabel.Text = me.Rank ?? "Rookie";
        MyXpLabel.Text = $"{me.Xp} XP";
    }

    try
    {
        var result = await _api.GetLeaderboard(20);
        if (result != null)
        {
            var json = JsonConvert.SerializeObject(result);
            var list = Newtonsoft.Json.Linq.JArray.Parse(json);

            for (int i = 0; i < list.Count; i++)
            {
                var player = list[i];
                var username = player["username"]?.ToString() ?? "???";
                var xp = player["xp"]?.ToObject<int>() ?? 0;
                var level = player["level"]?.ToObject<int>() ?? 1;
                var rank = player["rank"]?.ToString() ?? "Rookie";
                var isMe = username == me?.Username;

                AddLeaderboardRow(i + 1, username, xp, level, rank, isMe);

                if (isMe)
                    MyRankLabel.Text = $"#{i + 1}";
            }
        }
        else
        {
            ErrorLabel.Text = "No hay jugadores aún";
            ErrorLabel.IsVisible = true;
        }
    }
    catch (Exception ex)
    {
        ErrorLabel.Text = $"Error: {ex.Message}";
        ErrorLabel.IsVisible = true;
    }
    finally
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
    }
}
    private void AddLeaderboardRow(int position, string username, int xp, int level, string rank, bool isMe)
    {
        var medal = position switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"#{position}"
        };

        var bgColor = isMe ? Color.FromArgb("#0D1F0D") : Color.FromArgb("#12122A");
        var strokeColor = isMe ? Color.FromArgb("#00FF88") : Color.FromArgb("#333355");

        var container = new Border
        {
            BackgroundColor = bgColor,
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = isMe ? 2 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(50) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Padding = new Thickness(12, 10)
        };

        var posLabel = new Label
        {
            Text = medal,
            FontSize = position <= 3 ? 22 : 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = isMe ? Color.FromArgb("#00FF88") : Colors.White,
            VerticalOptions = LayoutOptions.Center
        };

        var infoStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        infoStack.Children.Add(new Label
        {
            Text = username + (isMe ? " (Tú)" : ""),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = isMe ? Color.FromArgb("#00FF88") : Colors.White
        });
        infoStack.Children.Add(new Label
        {
            Text = $"Nivel {level} • {rank}",
            FontSize = 12,
            TextColor = Color.FromArgb("#888899")
        });

        var xpLabel = new Label
        {
            Text = $"{xp:N0} XP",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#00FF88"),
            VerticalOptions = LayoutOptions.Center
        };

        grid.Add(posLabel, 0, 0);
        grid.Add(infoStack, 1, 0);
        grid.Add(xpLabel, 2, 0);

        container.Content = grid;
        LeaderboardStack.Children.Add(container);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}