using Raylib_cs;

public class Program
{
    // Размер окна из TASK.md §2.4 (легко поменять на 800×600 для единства с другими болванками)
    private const int WindowWidth = 320;
    private const int WindowHeight = 240;

    public static void Main()
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, "Сапёр");

        SetWindowIcon();

        // Основной цикл. Raylib опрашивает события ОС в BeginDrawing/EndDrawing,
        // поэтому цикл обязателен даже для пустого окна — без него окно «зависнет».
        // WindowShouldClose() возвращает true и по кнопке закрытия окна, и по Esc:
        // клавиша выхода задаётся SetExitKey и по умолчанию — KEY_ESCAPE.
        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            // Отрисовка поля появится на игровой фазе.

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    private static void SetWindowIcon()
    {
        // raylib не декодирует .ico: LoadImageFromMemory поддерживает только
        // png/bmp/tga/jpg/gif/pic/ppm/pgm/psd (проверено по исходнику rtextures.c
        // raylib 6.0). Поэтому иконкой окна служит встроенный PNG (как в других
        // болванках), а Resources/icon.ico остаётся иконкой exe-файла через
        // <ApplicationIcon> в csproj.
        Image icon = Raylib.LoadImageFromMemory(".png", Assets.GetPng("bomb"));

        if (icon.Width != 0)
        {
            Raylib.SetWindowIcon(icon);
            Raylib.UnloadImage(icon);
        }
    }
}
