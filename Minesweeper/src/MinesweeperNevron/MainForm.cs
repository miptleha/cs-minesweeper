using Nevron.Nov.UI;
using Nevron.Nov.Windows.Forms;

namespace MinesweeperNevron
{
    /// <summary>
    /// Пустое окно болванки с заголовком «Сапёр».
    /// </summary>
    /// <remarks>Эталон: при размножении заменяется базовый класс
    /// (KryptonForm / MaterialForm / Sunny.UI.UIForm / Form) и пакет в csproj.</remarks>
    internal class MainForm : Form
    {
        public MainForm()
        {
            Text = "Сапёр";
            ClientSize = new Size(800, 600);

            // NOV-виджет внутри WinForms-хоста (официальная интеграция NOV).
            var novLabel = new NLabel("Hello from NOV");
            novLabel.TextAlignment = Nevron.Nov.ENContentAlignment.MiddleCenter;
            var host = new NNovWidgetHost<NLabel>(novLabel) { Dock = DockStyle.Fill };
            Controls.Add(host);

            try
            {
                var ico = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null)
                    Icon = ico;
            }
            catch
            {
                // иконка не критична для работы окна
            }
        }
    }
}
