using LinguaBotArena.Services;
using Newtonsoft.Json;

namespace LinguaBotArena.Views;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _api = SessionService.Instance.Api;

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void LoginButton_Clicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        // Validar campos vacíos
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorLabel.Text = "⚠️ Por favor llena todos los campos";
            ErrorLabel.IsVisible = true;
            return;
        }

        // Mínimo 3 caracteres en usuario
        if (username.Length < 3)
        {
            ErrorLabel.Text = "⚠️ El usuario debe tener al menos 3 caracteres";
            ErrorLabel.IsVisible = true;
            return;
        }

        // No permitir espacios en usuario
        if (username.Contains(' '))
        {
            ErrorLabel.Text = "⚠️ El usuario no puede contener espacios";
            ErrorLabel.IsVisible = true;
            return;
        }

        // No permitir espacios en contraseña
        if (password.Contains(' '))
        {
            ErrorLabel.Text = "⚠️ La contraseña no puede contener espacios";
            ErrorLabel.IsVisible = true;
            return;
        }

        LoginButton.IsEnabled = false;
        LoginButton.Text = "Entrando...";

        try
        {
            var result = await _api.Login(username, password);

            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                var token = JsonConvert.DeserializeObject<LinguaBotArena.Models.TokenResponse>(json);

                if (token != null)
                {
                    SessionService.Instance.SetUser(token);
                    SessionService.Instance.Api.SetToken(token.AccessToken);
                    await Shell.Current.GoToAsync("//MainMenuPage");
                }
            }
            else
            {
                ErrorLabel.Text = "⚠️ Usuario o contraseña incorrectos";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception)
        {
            ErrorLabel.Text = "⚠️ Error conectando con el servidor";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "ENTRAR AL ARENA";
        }
    }

    private async void RegisterButton_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("RegisterPage");
    }
}