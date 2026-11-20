// Болванка (Duxel): пустое окно immediate-mode GUI с заголовком «Сапёр» и иконкой.
// Duxel — ImGui-style фреймворк на Vulkan с Windows-бэкендом: окно создаётся
// через DuxelWindowsApp.Run (пакет Duxel.Windows.App), а каждый кадр экран
// перерисовывается в методе Render (TASK.md §2.6).

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

// DoD группы A: хотя бы один встроенный PNG-ассет реально читается из ресурсов.
// Duxel декодирует изображения через Windows Imaging Component — для болванки
// достаточно факта чтения байтов.
try
{
    _ = Assets.GetPng("smiley");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
}

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Сапёр",

        // Размер как в болванках на Silk.NET (стандарт Duxel — 1280x720).
        Width = 800,
        Height = 600,

        // Иконку окна Duxel загружает сам: бэкенд разбирает байты .ico
        // (ICONDIR + ICONDIRENTRY) и создаёт большую и малую HICON через
        // CreateIconFromResourceEx — embedded-подход группы A, без файлов на диске.
        IconData = Assets.GetIco("icon")
    },
    Screen = new MinesweeperScreen()
});

/// <summary>Экран болванки: пустое окно без виджетов.</summary>
public sealed class MinesweeperScreen : UiScreen
{
    public override void Render(UiImmediateContext ui)
    {
        // Пустое окно болванки — виджетов нет; Duxel отрисовывает фон Vulkan'ом.
    }
}
