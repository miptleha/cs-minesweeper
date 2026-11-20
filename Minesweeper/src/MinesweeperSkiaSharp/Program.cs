using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;

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
    }

    private void SetWindowIcon()
    {
        using SKBitmap source = Assets.GetPng("bomb");

        // Гарантированно приводим к RGBA8888 — конвертацию делает Skia
        using SKBitmap rgba = source.Copy(SKColorType.Rgba8888);

        var rawImage = new RawImage(rgba.Width, rgba.Height, new Memory<byte>(rgba.Bytes));
        _window.SetWindowIcon(ref rawImage);
    }

    // These two methods are unused for this tutorial, aside from the logging we added earlier.
    private void OnUpdate(double deltaTime)
    {
        //Console.WriteLine("Update!");
    }

    private void OnRender(double deltaTime)
    {
        //Console.WriteLine("Render!");
    }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        if (key == Key.Escape)
            _window.Close();
    }
}
