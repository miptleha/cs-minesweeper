
using Hawkynt.NativeForms;
using Hawkynt.NativeForms.Backends;
using Hawkynt.NativeForms.Backends.Windows;

namespace MinesweeperNativeForms
{
    /// <summary>Точка входа болванки: окно появится на фазе реализации фреймворка (§6.x).</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Проверка загрузки embedded-ассета (DoD болванки).
            try
            {
                _ = Assets.GetPng("smiley");
            }
            catch
            {
                // Ассет не вшит — для болванки не критично.
            }

            BackendRegistry.Register(new Win32Backend());
            Application.Run(new MainForm());
        }
    }
}
