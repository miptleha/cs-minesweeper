using AntdUI;

namespace MinesweeperAntdUI
{
    /// <summary>
    /// Пустое окно болванки с заголовком «Сапёр».
    /// </summary>
    /// <remarks>Эталон: при размножении заменяется базовый класс
    /// (KryptonForm / MaterialForm / Sunny.UI.UIForm / Form) и пакет в csproj.
    /// У AntdUI заголовок — контрол PageHeader поверх borderless-окна;
    /// фоном полосы управляет BackExtend (два одинаковых цвета = сплошной цвет).</remarks>
    internal class MainForm : Window
    {
        public MainForm()
        {
            Text = "Сапёр";
            ClientSize = new Size(800, 600);

            var header = new PageHeader
            {
                Dock = DockStyle.Top,
                Text = "Сапёр",
                ShowButton = true,          // свернуть / развернуть / закрыть
                ShowIcon = true,            // иконка слева в шапке
                Height = 32,                // системная высота, компактно
                BackExtend = "#f3f3f3,#f3f3f3"  // сплошной светло-серый, как шапка Windows
            };
            // Явные цвета вместо темы: тёмный текст и тёмные иконки кнопок
            // на светлой полосе, независимо от светлой/тёмной темы ОС.
            header.UseSystemStyleColor = true;
            header.ForeColor = Color.FromArgb(32, 32, 32);
            header.UseForeColorDrawIcons = true;
            header.Font = new Font("Segoe UI", 9F);
            Controls.Add(header);

            try
            {
                using var ms = new MemoryStream(Assets.GetPng("bomb"));
                header.Icon = new Bitmap(ms);
            }
            catch
            {
                // без иконки в шапке не критично
            }

            try
            {
                var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
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
