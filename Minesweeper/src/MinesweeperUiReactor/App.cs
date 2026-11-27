using System.IO;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;   // BackdropKind, Component, Element
using static Microsoft.UI.Reactor.Factories;

// DoD группы A: хотя бы один PNG-ассет реально читается при запуске.
try
{
    _ = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "smiley.png"));
}
catch
{
    // ассет не критичен для болванки
}

// Иконка окна: .ico лежит рядом с exe (см. шаг 3).
WindowIcon? icon = null;
try
{
    icon = WindowIcon.FromPath(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
}
catch
{
    // иконка не критична для болванки
}

// Точка входа Reactor: заголовок, размеры, иконка окна.
ReactorApp.Run<App>("Сапёр", width: 800, height: 600, icon: icon);

class App : Component
{
    public override Element Render()
    {
        return VStack(
            TitleBar("Сапёр"),
            VStack(8,
                Heading("Сапёр"),
                Body("Болванка на Microsoft UI Reactor: доска и логика — на следующей фазе.")
            ).Padding(24)
        ).Backdrop(BackdropKind.Mica);
    }
}
