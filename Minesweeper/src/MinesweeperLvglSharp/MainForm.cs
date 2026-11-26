using LVGLSharp.Drawing;
using LVGLSharp.Forms;

namespace MinesweeperLvglSharp
{
    /// <summary>Окно болванки на LVGLSharp.Forms (WinForms-модель, рендер LVGL).</summary>
    internal class MainForm : Form
    {
        public MainForm()
        {
            Text = "Сапёр";
            ClientSize = new Size(800, 600);  // тип Size сверить при сборке: может быть свой тип библиотеки
            // TODO: игровое поле — следующая фаза
        }
    }
}
