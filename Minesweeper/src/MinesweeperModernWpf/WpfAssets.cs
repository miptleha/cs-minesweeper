using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MinesweeperModernWpf
{
    /// Загрузка PNG-ассетов через pack URI (Resource-элементы csproj).
    internal static class WpfAssets
    {
        /// Возвращает BitmapFrame PNG-ассета: pack://application:,,,/<сборка>;component/Resources/<name>.png
        public static BitmapFrame GetPng(string name)
        {
            var assembly = typeof(WpfAssets).Assembly.GetName().Name;
            var uri = new Uri($"pack://application:,,,/{assembly};component/Resources/{name}.png", UriKind.Absolute);
            return BitmapFrame.Create(uri);
        }
    }
}
