using LinguaBotArena.Services;
using Newtonsoft.Json;

namespace LinguaBotArena.Views;

public partial class RegisterPage : ContentPage
{
private readonly ApiService _api = SessionService.Instance.Api;
    private string _selectedColor = "#00FF88";

    public RegisterPage()
    {
        InitializeComponent();
    }

    private void OnColorSelected(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            _selectedColor = btn.BackgroundColor.ToHex();
            RobotPreview.TextColor = btn.BackgroundColor;
        }
    }

private async void OnRegisterClicked(object sender, EventArgs e)
{
    ErrorLabel.IsVisible = false;

    if (string.IsNullOrWhiteSpace(UsernameEntry.Text) ||
        string.IsNullOrWhiteSpace(EmailEntry.Text) ||
        string.IsNullOrWhiteSpace(PasswordEntry.Text))
    {
        ErrorLabel.Text = "Por favor llena todos los campos";
        ErrorLabel.IsVisible = true;
        return;
    }

    RegisterButton.IsEnabled = false;
    RegisterButton.Text = "Creando cuenta...";

    try
    {
        var result = await _api.Register(
            UsernameEntry.Text.Trim(),
            EmailEntry.Text.Trim(),
            PasswordEntry.Text,
            _selectedColor
        );

        if (result != null)
        {
            var json = JsonConvert.SerializeObject(result);
            var token = JsonConvert.DeserializeObject<LinguaBotArena.Models.TokenResponse>(json);

            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                // Guardar token en TODOS lados
                SessionService.Instance.SetUser(token);
                SessionService.Instance.Api.SetToken(token.AccessToken);
                Preferences.Set("access_token", token.AccessToken);
                
                await Shell.Current.GoToAsync("//MainMenuPage");
            }
            else
            {
                ErrorLabel.Text = "Error: token vacío";
                ErrorLabel.IsVisible = true;
            }
        }
        else
        {
            ErrorLabel.Text = "Error al crear la cuenta";
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
        RegisterButton.IsEnabled = true;
        RegisterButton.Text = "CREAR CUENTA";
    }
}

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}