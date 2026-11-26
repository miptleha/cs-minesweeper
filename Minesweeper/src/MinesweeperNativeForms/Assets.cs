using System.Reflection;

namespace MinesweeperNativeForms
{
    /// <summary>
    /// Доступ к встроенным PNG-ассетам.
    /// </summary>
    internal static class Assets
    {
        /// <summary>Возвращает содержимое встроенного PNG из Resources.</summary>
        /// <param name="name">Имя файла без расширения, например "smiley".</param>
        public static byte[] GetPng(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Resources.{name}.png")
                ?? throw new InvalidOperationException($"Embedded resource Resources.{name}.png not found");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
