using CoreWindowsWrapper;

namespace MinesweeperCoreWindows
{
    /// <summary>Окно болванки на CoreWindowsWrapper (нативные Win32-контролы).</summary>
    internal class MainForm : NativeWindow
    {
        public MainForm()
        {
            Text = "Сапёр";
            Width = 800;
            Height = 600;

            IconFile = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");

            // TODO: игровое поле — следующая фаза; иконку окна сверить (свойства IconFile/IconId)
        }
    }
}
