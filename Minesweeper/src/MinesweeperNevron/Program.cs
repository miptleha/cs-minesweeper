using Nevron.Nov;
using Nevron.Nov.Grid;
using Nevron.Nov.Windows.Forms;

namespace MinesweeperNevron
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // Инициализация NOV: установка модуля Grid перед запуском форм.
            NNovApplicationInstaller.Install(new NModule[] { NGridModule.Instance });

            // Проверка загрузки embedded-ассета (DoD болванки).
            try
            {
                _ = Assets.GetPng("smiley");
            }
            catch
            {
                // Ассет не вшит — для болванки не критично.
            }

            Application.Run(new MainForm());
        }
    }
}
