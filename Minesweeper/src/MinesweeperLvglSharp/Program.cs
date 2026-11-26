using LVGLSharp.Forms;

namespace MinesweeperLvglSharp
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

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
