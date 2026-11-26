using Jalium.UI;
using Jalium.UI.Controls;
using MinesweeperJaliumUi;

var builder = AppBuilder.CreateBuilder(args);
builder.ConfigureApplication(app =>
{
    app.MainWindow = new Window
    {
        Title = "Сапёр",
        Width = 960,
        Height = 640
    };
});

// DoD: минимум один ассет реально загружается
var _ = Assets.GetPng("smiley");


using var jalium = builder.Build();
return jalium.Run();
