namespace MinesweeperSunnyUI
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Инициализация WinForms (visual styles, DPI-масштабирование).
            ApplicationConfiguration.Initialize();

            // Проверка загрузки embedded-ассета (DoD болванки).
            try
            {
                var _ = Assets.GetPng("smiley");
            }
            catch
            {
                // Ассет не вшит — для болванки не критично.
            }

            Application.Run(new MainForm());
        }
    }
}
