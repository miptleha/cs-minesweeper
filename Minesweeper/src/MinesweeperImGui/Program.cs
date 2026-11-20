using ImGuiNET;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using StbImageSharp;

public class Program
{
    private readonly IWindow _window;

    public Program()
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(800, 600),
            Title = "Сапёр"
        };

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
    }

    public static void Main()
    {
        new Program().Run();
    }

    private void Run()
    {
        _window.Run();
    }

    private void OnLoad()
    {
        Console.WriteLine("Load!");

        IInputContext input = _window.CreateInput();
        for (int i = 0; i < input.Keyboards.Count; i++)
            input.Keyboards[i].KeyDown += KeyDown;

        SetWindowIcon();

        // Минимальная инициализация ImGui: создаём контекст.
        // Полный рендерер (ImGuiController с шрифтами и GL-рендерингом)
        // подключается на этапе игровой фазы, болванке он не нужен.
        ImGui.CreateContext();
    }

    private void SetWindowIcon()
    {
        // Декодируем встроенный PNG в RGBA8888 через StbImageSharp
        // (порт stb_image.h; именно его рекомендуют туториалы Silk.NET
        // для загрузки изображений вместе с Silk.NET).
        ImageResult image = ImageResult.FromMemory(
            Assets.GetPng("bomb"), ColorComponents.RedGreenBlueAlpha);

        var rawImage = new RawImage(image.Width, image.Height, new Memory<byte>(image.Data));
        _window.SetWindowIcon(ref rawImage);
    }

    private void OnUpdate(double deltaTime)
    {
        // Обновление игровой логики появится на этапе разработки игры.
    }

    private void OnRender(double deltaTime)
    {
        // Отрисовка поля и ImGui-оверлея появится на этапе разработки игры.
    }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        if (key == Key.Escape)
            _window.Close();
    }
}
