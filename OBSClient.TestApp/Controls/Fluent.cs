using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Exceptions;

namespace OBSStudioClient.TestApp.Controls;

/// <summary>
/// Looks up the styles of the app (App.xaml) and of Fluent 2 (the Windows App SDK) for the controls that are built in code.
/// Colors always come from styles that use theme resources, so they follow the light, dark and high contrast themes.
/// </summary>
internal static class Fluent
{
    /// <summary>The unit of the spacing grid of Fluent 2.</summary>
    public const double Unit = 4;

    /// <summary>Gets a style by key, or null when there is none (the control then keeps its default look).</summary>
    public static Style? StyleOf(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;

    /// <summary>Creates text in one of the styles of the type ramp, such as BodyStrongTextBlockStyle.</summary>
    public static TextBlock Text(string text, string styleKey, bool wrap = true)
    {
        var block = new TextBlock { Text = text, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        if (StyleOf(styleKey) is { } style)
        {
            block.Style = style;
        }

        return block;
    }

    /// <summary>Creates a line of secondary text in the caption size.</summary>
    public static TextBlock Caption(string text) => Text(text, "SecondaryCaptionStyle");

    /// <summary>Creates a panel with the look of a card of Fluent 2: a layer with a thin border and rounded corners.</summary>
    public static Border Card(UIElement content)
    {
        var card = new Border { Child = content };
        if (StyleOf("CardStyle") is { } style)
        {
            card.Style = style;
        }

        return card;
    }

    /// <summary>Shows on an info bar that something has to be corrected before a request can be sent.</summary>
    public static void ShowWarning(InfoBar bar, string title, string message)
    {
        bar.Severity = InfoBarSeverity.Warning;
        bar.Title = title;
        bar.Message = message;
        bar.IsOpen = true;
    }

    /// <summary>Shows on an info bar why a request or a batch did not succeed, in the words of the one who refused it.</summary>
    public static void ShowFailure(InfoBar bar, Exception error)
    {
        bar.Severity = error is ObsResponseException ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        bar.Title = error switch
        {
            ObsResponseException => "OBS Studio refused the request",
            ObsClientException => "The client could not complete the request",
            ArgumentException or NotSupportedException => "The client refused the arguments",
            _ => error.GetType().Name,
        };
        bar.Message = error is ObsResponseException response
            ? $"{response.ErrorCode} ({(int)response.ErrorCode}): {response.ErrorMessage ?? "no further explanation"}"
            : error.Message;
        bar.IsOpen = true;
    }
}
