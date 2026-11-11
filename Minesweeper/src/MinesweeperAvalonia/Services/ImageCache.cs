using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MinesweeperAvalonia.Services;

/// <summary>
/// Статический кэш картинок для Avalonia UI.
/// Загружает PNG-ассеты из ресурсов один раз и хранит в словаре.
/// </summary>
public static class ImageCache
{
    private static readonly Dictionary<string, Bitmap> _cache = new();

    /// <summary>
    /// Получает картинку из кэша или загружает из ресурсов.
    /// </summary>
    /// <param name="name">Имя ассета без расширения (например, "cell_closed").</param>
    /// <returns>Загруженный Bitmap.</returns>
    public static Bitmap Get(string name)
    {
        if (_cache.TryGetValue(name, out var bitmap))
            return bitmap;

        bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://MinesweeperAvalonia/Resources/{name}.png")));
        _cache[name] = bitmap;
        return bitmap;
    }
}
