using ReaLTaiizor.Enum.Material;
using ReaLTaiizor.Forms;

namespace MinesweeperReaLTaiizor
{
    /// <summary>
    /// Пустое окно болванки с заголовком «Сапёр».
    /// </summary>
    /// <remarks>Эталон: при размножении заменяется базовый класс
    /// (KryptonForm / MaterialForm / Sunny.UI.UIForm / Form) и пакет в csproj.</remarks>
    internal class MainForm : MaterialForm
    {
        public MainForm()
        {
            Text = "Сапёр";
            FormStyle = FormStyles.ActionBar_None;
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
