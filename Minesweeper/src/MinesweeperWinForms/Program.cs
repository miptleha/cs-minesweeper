namespace MinesweeperWinForms;

static class Program
{
    /// <summary>
    ///  Точка входа приложения.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Настройка конфигурации приложения (например, DPI или шрифт по умолчанию).
        // См. https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }    
}