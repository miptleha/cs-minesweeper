using Eto.Forms;

namespace MinesweeperEto
{
    /// <summary>Окно болванки на Eto.Forms (платформа WinForms).</summary>
    internal class MainForm : Form
    {
        public MainForm()
        {
            Title = "Сапёр";
            ClientSize = new Eto.Drawing.Size(800, 600);

            var stream = typeof(Program).Assembly.GetManifestResourceStream("Resources.icon.ico");
            if (stream != null) Icon = new Eto.Drawing.Icon(stream);

            // TODO: игровое поле — следующая фаза
        }
    }
}
