using System.Net;
using System.Text;
using tPoseidon.DrillEgg.Art;
using tPoseidon.DrillEgg.Connectors;

namespace tPoseidon.DrillEgg.Web;

internal static class Pages
{
    public const string FormTokenMeta = "shop-form";
    public const string Title = "Drill Egg";

    public static string Game(string formToken, string account, bool canSignOut)
    {
        var bar = new StringBuilder();
        bar.Append("<div class=\"bar\"><span>Playing as <b>").Append(E(account)).Append("</b></span>");
        if (canSignOut) bar.Append("<form method=\"post\" action=\"/sign-out\"><button type=\"submit\" class=\"link\">Sign out</button></form>");
        bar.Append("</div>");
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="X-UA-Compatible" content="IE=edge">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="{{FormTokenMeta}}" content="{{E(formToken)}}">
            <title>{{Title}}</title>
            <link rel="stylesheet" href="/gacha/drill.css">
            <link rel="stylesheet" href="/site.css">
            </head>
            <body class="game">
            <main class="frame"><canvas id="stage" width="640" height="480"></canvas></main>
            {{bar}}
            <script src="/gacha/player.js"></script>
            <script src="/gacha/drill.js"></script>
            </body>
            </html>
            """;
    }

    public static string SignIn(SignInKind kind, string? message, string? account)
    {
        var body = new StringBuilder();
        body.Append("<h1>Drill Egg</h1>");
        if (message is not null) body.Append("<p class=\"error\" role=\"alert\">").Append(E(message)).Append("</p>");
        switch (kind)
        {
            case SignInKind.Token:
                body.Append("<p>Open the Drill Egg with the link your game server gives you. The link signs you in.</p>");
                break;
            case SignInKind.Name:
                body.Append("<p>Type a player name to start. Your coupons and prizes are kept under this name.</p>")
                    .Append("<form method=\"post\" action=\"/sign-in\">")
                    .Append("<label for=\"account\">Player name</label>")
                    .Append("<input id=\"account\" name=\"account\" maxlength=\"").Append(PlayerNames.MaxLength).Append("\" autocomplete=\"username\" required autofocus value=\"").Append(E(account ?? string.Empty)).Append("\">")
                    .Append("<button type=\"submit\">Play</button></form>");
                break;
            default:
                body.Append("<p>Sign in with your game account.</p>")
                    .Append("<form method=\"post\" action=\"/sign-in\">")
                    .Append("<label for=\"account\">Account</label>")
                    .Append("<input id=\"account\" name=\"account\" autocomplete=\"username\" required autofocus value=\"").Append(E(account ?? string.Empty)).Append("\">")
                    .Append("<label for=\"password\">Password</label>")
                    .Append("<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"current-password\" required>")
                    .Append("<button type=\"submit\">Sign in</button></form>");
                break;
        }
        return Card("Sign in", body.ToString());
    }

    public static string Setup(ArtSetup art)
    {
        ArgumentNullException.ThrowIfNull(art);
        var body = new StringBuilder();
        body.Append("<h1>One more step</h1>");
        if (art.State == ArtState.Failed && art.Problem is { } problem)
        {
            body.Append("<p class=\"error\" role=\"alert\">").Append(E(problem)).Append("</p>");
        }
        else
        {
            body.Append("<p>The Drill Egg needs the game's own art, which is not included. It is made from the file <b>")
                .Append(E(Path.GetFileName(art.SwfPath))).Append("</b> of your copy of the Drill Egg gacha.</p>");
        }
        body.Append("<ol>")
            .Append("<li>Find <b>myshop.swf</b> in your copy of the Drill Egg gacha files.</li>")
            .Append("<li>Copy it to <code>").Append(E(art.SwfPath)).Append("</code></li>")
            .Append("<li>Press <b>Check again</b>. The art is made once, in a few seconds.</li>")
            .Append("</ol>")
            .Append("<form method=\"get\" action=\"/\"><button type=\"submit\">Check again</button></form>");
        return Card("Setup", body.ToString());
    }

    public static string Problem(string heading, string message) =>
        Card(heading, $"<h1>{E(heading)}</h1><p class=\"error\" role=\"alert\">{E(message)}</p><form method=\"get\" action=\"/\"><button type=\"submit\">Try again</button></form>");

    private static string Card(string title, string body) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{E(title)}} - {{Title}}</title>
        <link rel="stylesheet" href="/site.css">
        </head>
        <body class="page">
        <main class="card">
        {{body}}
        </main>
        </body>
        </html>
        """;

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
