using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;

namespace LiveSplit.WsServer.State;

/// <summary>
///     Caches the PNG data URI of each icon, so icons are encoded once instead of
///     on every broadcast. Entries go away together with their <see cref="Image"/>.
/// </summary>
public sealed class IconCache
{
    private readonly ConditionalWeakTable<Image, string> cache = new();

    public string GetDataUri(Image image)
    {
        if (image == null)
        {
            return null;
        }

        if (cache.TryGetValue(image, out string cached))
        {
            return cached;
        }

        string encoded = Encode(image);
        if (encoded != null)
        {
            cache.Add(image, encoded);
        }

        return encoded;
    }

    public static string Encode(Image image)
    {
        if (image == null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        }
        catch (Exception)
        {
            // A broken image must not prevent the state from being sent.
            return null;
        }
    }
}
