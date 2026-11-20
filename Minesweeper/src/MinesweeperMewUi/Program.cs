// Болванка (MewUI): пустое окно code-first GUI с заголовком «Сапёр» и иконкой.
// MewUI — кроссплатформенный фреймворк с fluent C# markup (без XAML), заточен
// под NativeAOT. Перед Application.Run нужно явно зарегистрировать платформенный
// хост и рендер-бэкенд — MewUI не выбирает их сам (TASK.md §2.8, README MewUI).

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

// DoD группы A: хотя бы один встроенный PNG-ассет реально читается из ресурсов.
try
{
    _ = Assets.GetPng("smiley");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
}

// Платформенный хост Win32 + бэкенд Direct2D (рекомендованный по умолчанию
// для Windows; альтернативы — GdiBackend.Register() и MewVGWin32Backend.Register()).
Win32Platform.Register();
Direct2DBackend.Register();

var window = new Window()
    .Title("Сапёр")
    .Fixed(800, 600)
    // Иконка окна: IconSource разбирает байты .ico (ICONDIR + ICONDIRENTRY),
    // поддерживает PNG- и DIB-записи внутри .ico и сам выбирает слой,
    // ближайший к нужному размеру. Embedded-подход группы A.
    .Icon(IconSource.FromBytes(Assets.GetIco("icon")));

Application.Run(window);
