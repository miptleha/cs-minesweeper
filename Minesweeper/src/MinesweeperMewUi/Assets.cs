using System.Reflection;

/// <summary>
/// Загрузчик ассетов, встроенных как ресурсы
/// (LogicalName="Resources.%(Filename)%(Extension)").
/// </summary>
public static class Assets
{
    /// <summary>
    /// Читает встроенный PNG-ассет целиком, в виде исходных байтов файла.
    /// Имя — без расширения: Assets.GetPng("smiley") → ресурс Resources.smiley.png.
    /// На игровой фазе пиксели можно декодировать средствами MewUI
    /// (ImageSource.FromPngBytes) или оставить как есть.
    /// </summary>
    public static byte[] GetPng(string name) => ReadResource($"Resources.{name}.png");

    /// <summary>
    /// Читает встроенный .ico-ассет целиком, в виде исходных байтов файла.
    /// Имя — без расширения: Assets.GetIco("icon") → ресурс Resources.icon.ico.
    /// Байты передаются в IconSource.FromBytes — MewUI сам разбирает ICONDIR
    /// и выбирает ближайший слой иконки.
    /// </summary>
    public static byte[] GetIco(string name) => ReadResource($"Resources.{name}.ico");

    private static byte[] ReadResource(string resourceName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            string available = string.Join(", ",
                Assembly.GetExecutingAssembly().GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Встроенный ресурс '{resourceName}' не найден. Доступные: {available}");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
