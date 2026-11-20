using System.Reflection;

/// <summary>
/// Загрузчик PNG-ассетов, встроенных как ресурсы
/// (LogicalName="Resources.%(Filename)%(Extension)").
/// </summary>
public static class Assets
{
    /// <summary>
    /// Читает встроенный PNG-ассет целиком, в виде исходных байтов файла.
    /// Имя — без расширения: Assets.GetPng("smiley") → ресурс Resources.smiley.png.
    /// Декодирование пикселей (например, в RGBA8888 через StbImageSharp)
    /// выполняется в месте использования — у каждого UI свой способ.
    /// </summary>
    public static byte[] GetPng(string name)
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

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
