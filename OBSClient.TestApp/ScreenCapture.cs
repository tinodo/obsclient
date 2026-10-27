using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OBSStudioClient.TestApp;

/// <summary>Takes a picture of what the app shows, for the smoke test: CI keeps the pictures, so the design can be looked at without running the app.</summary>
internal static class ScreenCapture
{
    /// <summary>Writes a picture of an element as a PNG file.</summary>
    /// <param name="element">The element, which has to be shown.</param>
    /// <param name="path">The file.</param>
    public static async Task SaveAsync(UIElement element, string path)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var pixels = await bitmap.GetPixelsAsync();

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels.ToArray());
        await encoder.FlushAsync();

        stream.Seek(0);
        using var input = stream.AsStreamForRead();
        using var output = File.Create(path);
        await input.CopyToAsync(output);
    }
}
