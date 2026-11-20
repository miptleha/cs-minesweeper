using System.Reflection;
using SkiaSharp;

/// <summary>
/// Загрузчик PNG-ассетов, встроенных как ресурсы
/// (LogicalName="Resources.%(Filename)%(Extension)").
/// </summary>
public static class Assets
{
    /// <summary>
    /// Читает и декодирует встроенный PNG-ассет.
    /// Имя — без расширения: Assets.GetPng("smiley") → ресурс Resources.smiley.png.
    /// </summary>
    public static SKBitmap GetPng(string name)
    {
        string resourceName = $"Resources.{name}.png";

        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            string available = string.Join(", ",
                Assembly.GetExecutingAssembly().GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Встроенный ресурс '{resourceName}' не найден. Доступные: {available}");
        }

        SKBitmap? bitmap = SKBitmap.Decode(stream);
        return bitmap
            ?? throw new InvalidOperationException($"Не удалось декодировать ресурс '{resourceName}'.");
    }
}
