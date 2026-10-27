using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace OBSStudioClient.TestApp.Controls;

/// <summary>Copies text to the clipboard. The clipboard can refuse (it is not available to an app that is not in the foreground), which must not take the app down.</summary>
internal static class ClipboardHelper
{
    /// <summary>Copies text, and tells on the button whether that worked, for a moment.</summary>
    /// <param name="button">The button that was pressed.</param>
    /// <param name="text">The text to copy.</param>
    public static async Task CopyAsync(Button button, string text)
    {
        var label = button.Content;
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            button.Content = "Copied";
        }
        catch (Exception)
        {
            button.Content = "Could not copy";
        }

        await Task.Delay(1500);
        button.Content = label;
    }
}
