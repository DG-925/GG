using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GG.Core.Logging;

namespace GG.App.Services;

/// <summary>Loads images from disk without locking the file, scaled down to save memory.</summary>
public static class ImageLoader
{
    /// <summary>Returns a frozen image, or null if the path is empty, missing or not an image.</summary>
    public static ImageSource? Load(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // read fully now so the file isn't kept open
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodePixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not load image {path}: {ex.Message}");
            return null;
        }
    }
}
