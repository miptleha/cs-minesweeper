using Jalium.UI;
using Jalium.UI.Controls;

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
using var jalium = builder.Build();
return jalium.Run();
