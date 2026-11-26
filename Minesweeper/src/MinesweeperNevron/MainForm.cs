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
