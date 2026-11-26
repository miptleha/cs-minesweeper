using System.Reflection;

namespace MinesweeperSiticone
{
    /// <summary>
    /// Доступ к PNG-ассетам, вшитым в сборку как ресурсы Resources.*.png.
    /// </summary>
    internal static class Assets
    {
        /// <summary>
        /// Возвращает содержимое PNG-ассета по имени без расширения.
        /// </summary>
        public static byte[] GetPng(string name)
        {
            var resource = $"Resources.{name}.png";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded-ресурс '{resource}' не найден.");
            using var output = new MemoryStream();
            stream.CopyTo(output);
            return output.ToArray();
        }
    }
}
