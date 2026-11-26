namespace MinesweeperAntdUI
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
                _ = Assets.GetPng("smiley");
            }
            catch
            {
                // Ассет не вшит — для болванки не критично.
            }

            AntdUI.Config.Theme().Dark("#2a2a2a", "#ffffff").Light("#ffffff", "#2a2a2a").FormBorderColor();
            Application.Run(new MainForm());
        }
    }
}
