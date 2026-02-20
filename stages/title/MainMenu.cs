using System;
using System.Diagnostics;
using System.Text;
using Godot;

public partial class MainMenu : Node3D
{
    private string usernameText = "";
    private string passwordText = "";
    private HttpRequest httpRequest;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.httpRequest = GetNode<HttpRequest>("HTTPRequest");
        Debug.Assert(this.httpRequest != null, "HTTPRequest can't be null");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }

    public void OnUsernameInputTextChanged(string text)
    {
        this.usernameText = text;
    }

    public void OnPasswordInputTextChanged(string text)
    {
        this.passwordText = text;
    }

    public void OnLoginButtonPressed()
    {
        string[] customHeaders = ["Content-Type: application/json"];
        string jsonBody =
            $"{{\"username\": \"{this.usernameText}\", \"password\": \"{this.passwordText}\"}}";

        GD.Print(jsonBody);
        Error err = this.httpRequest.Request(
            "http://90.28.104.14:1337/auth/login",
            customHeaders,
            HttpClient.Method.Post,
            jsonBody
        );

        if (err != Error.Ok)
        {
            GD.PrintErr("An error occurred creating the HTTP request.");
        }
    }

    private void OnHttpRequestRequestCompleted(
        long result,
        long responseCode,
        string[] headers,
        byte[] body
    )
    {
        // Check if the connection itself succeeded (DNS, Timeout, etc.)
        if (result != (long)HttpRequest.Result.Success)
        {
            GD.PrintErr($"Connection Error: {result}");
            return;
        }

        switch (responseCode)
        {
            case 200:
                {
                    string jsonString = Encoding.UTF8.GetString(body);

                    GD.Print("Success! Data received:");
                    GD.Print(jsonString);

                    var json = new Json();
                    json.Parse(jsonString);
                    var data = json.Data.AsGodotDictionary();
                    string token = (string)data["token"];
                }
                break;
            case 400:
                GD.PrintErr("Invalid credentials");
                break;
            default:
                GD.PrintErr($"HTTP Error: {responseCode}");
                break;
        }
    }
}
