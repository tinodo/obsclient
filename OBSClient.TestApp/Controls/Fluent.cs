using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
}
