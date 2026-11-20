// Болванка Phase 27 (TerraFX): пустое окно Win32 с заголовком «Сапёр» и иконкой.
// TerraFX.Interop.Windows — типизированные низкоуровневые привязки к Win32:
// окно создаётся вручную через RegisterClassW/CreateWindowExW и классический
// цикл сообщений GetMessageW/TranslateMessage/DispatchMessageW (TASK.md §2.5).

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

public static unsafe class Program
{
    // Размер окна болванки — как в болванках на Silk.NET.
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    public static int Main()
    {
        // DoD группы A: хотя бы один встроенный PNG-ассет реально читается из ресурсов.
        // TerraFX не декодирует PNG — для болванки достаточно факта чтения байтов.
        try
        {
            _ = Assets.GetPng("smiley");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }

        // hInstance текущего процесса — обязателен для класса окна.
        HINSTANCE hInstance = new HINSTANCE(GetModuleHandleW(null).Value);

        // Строки закреплены (fixed) на всё время регистрации класса и создания окна:
        // Win32 копирует имя класса, но обращается к указателю синхронно.
        fixed (char* lpszClassName = "MinesweeperTerraFxWindow")
        fixed (char* lpWindowName = "Сапёр")
        {
            var windowClass = new WNDCLASSW
            {
                style = CS.CS_HREDRAW | CS.CS_VREDRAW,
                lpfnWndProc = &WndProc,
                hInstance = hInstance,
                // Иконка-заглушка для класса окна; своей заменим ниже через WM_SETICON.
                hIcon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION),
                hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW),
                hbrBackground = new HBRUSH((void*)(nint)(COLOR.COLOR_WINDOW + 1)),
                lpszClassName = lpszClassName
            };
            _ = RegisterClassW(&windowClass);

            HWND hwnd = CreateWindowExW(
                0,
                lpszClassName,
                lpWindowName,
                (uint)WS.WS_OVERLAPPEDWINDOW,
                CW_USEDEFAULT,
                CW_USEDEFAULT,
                WindowWidth,
                WindowHeight,
                HWND.NULL,   // Родительского окна нет.
                HMENU.NULL,  // Меню не используем.
                hInstance,
                null);

            if (hwnd.Value == null)
            {
                Console.Error.WriteLine("Не удалось создать окно Win32.");
                return 1;
            }

            SetWindowIcon(hwnd);

            // Показ окна (без этого CreateWindowExW создаёт окно скрытым).
            _ = ShowWindow(hwnd, SW.SW_SHOW);

            // Цикл сообщений: GetMessageW блокируется до появления сообщения,
            // поэтому окно отзывчиво и не грузит процессор. GetMessageW возвращает
            // BOOL, где 0 — WM_QUIT, а -1 — ошибка; поэтому проверяем строго > 0.
            MSG msg;
            while (GetMessageW(&msg, HWND.NULL, 0, 0).Value > 0)
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }

            // Код выхода — wParam сообщения WM_QUIT.
            return (int)msg.wParam;
        }
    }

    private static void SetWindowIcon(HWND hwnd)
    {
        // Загружаем иконку из встроенного Resources/icon.ico.
        // Важно: у LoadImage НЕТ флага «из памяти» — LR_LOADFROMFILE работает
        // только с файлом на диске. Поэтому .ico разбираем вручную (ICONDIR +
        // таблица ICONDIRENTRY) и создаём HICON через CreateIconFromResourceEx,
        // который принимает биты записи RT_ICON (BMP- и PNG-сжатые форматы).
        byte[] iconBytes = Assets.GetIco("icon");

        HICON hIconSmall = TryCreateIconFromIco(iconBytes, desiredSize: 16);
        HICON hIconBig = TryCreateIconFromIco(iconBytes, desiredSize: 32);

        if (hIconSmall.Value == null || hIconBig.Value == null)
        {
            // Запасной вариант: системная иконка класса окна (IDI_APPLICATION);
            // иконка exe-файла из <ApplicationIcon> остаётся видимой в проводнике.
            Console.Error.WriteLine(
                "Не удалось разобрать Resources/icon.ico; использую системную иконку IDI_APPLICATION.");
            hIconSmall = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
            hIconBig = hIconSmall;
        }

        // ICON_SMALL — иконка в заголовке окна, ICON_BIG — в панели задач и Alt+Tab.
        _ = SendMessageW(hwnd, (uint)WM.WM_SETICON, new WPARAM((nuint)ICON_SMALL), new LPARAM((nint)hIconSmall.Value));
        _ = SendMessageW(hwnd, (uint)WM.WM_SETICON, new WPARAM((nuint)ICON_BIG), new LPARAM((nint)hIconBig.Value));

        // HICON намеренно не уничтожаем (DestroyIcon): им владеет окно до закрытия.
    }

    // Разбирает .ico-файл и создаёт HICON наиболее близкого к desiredSize размера.
    // Формат файла: заголовок ICONDIR — 2 байта reserved (всегда 0), 2 байта type
    // (1 — иконка, 2 — курсор), 2 байта count; далее count записей ICONDIRENTRY
    // по 16 байт: width(1), height(1), colorCount(1), reserved(1), planes(2),
    // bitCount(2), bytesInRes(4), imageOffset(4). Тело записи — те самые биты
    // RT_ICON, которые CreateIconFromResourceEx превращает в HICON.
    private static HICON TryCreateIconFromIco(byte[] iconBytes, int desiredSize)
    {
        if (iconBytes.Length < 6 + 16)
        {
            return default;
        }

        ReadOnlySpan<byte> span = iconBytes;
        ushort reserved = BinaryPrimitives.ReadUInt16LittleEndian(span);
        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4));

        if (reserved != 0 || type != 1 || count == 0)
        {
            return default;
        }

        // Выбираем запись, наиболее близкую по размеру к desiredSize.
        int bestIndex = -1;
        uint bestDiff = uint.MaxValue;
        for (int i = 0; 6 + 16 * (i + 1) <= iconBytes.Length && i < count; i++)
        {
            ReadOnlySpan<byte> entry = span.Slice(6 + 16 * i, 16);
            int width = entry[0] == 0 ? 256 : entry[0]; // 0 в поле означает 256 px
            int height = entry[1] == 0 ? 256 : entry[1];

            uint diff = (uint)(Math.Abs(width - desiredSize) + Math.Abs(height - desiredSize));
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return default;
        }

        ReadOnlySpan<byte> best = span.Slice(6 + 16 * bestIndex, 16);
        uint bytesInRes = BinaryPrimitives.ReadUInt32LittleEndian(best.Slice(8));
        uint imageOffset = BinaryPrimitives.ReadUInt32LittleEndian(best.Slice(12));

        if (bytesInRes == 0
            || imageOffset >= (uint)iconBytes.Length
            || bytesInRes > (uint)iconBytes.Length - imageOffset)
        {
            return default;
        }

        fixed (byte* pData = iconBytes)
        {
            // dwVer = 0x00030000 — стандартная версия формата иконок;
            // cxDesired/cyDesired = desiredSize — система сама масштабирует запись.
            return CreateIconFromResourceEx(
                pData + imageOffset,
                bytesInRes,
                BOOL.TRUE,
                0x00030000,
                desiredSize,
                desiredSize,
                (uint)LR.LR_DEFAULTCOLOR);
        }
    }

    // Обработчик сообщений окна. Обязателен [UnmanagedCallersOnly]:
    // указатель на managed-метод без этого атрибута нельзя записать в lpfnWndProc.
    [UnmanagedCallersOnly]
    private static LRESULT WndProc(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
            case WM.WM_DESTROY:
                // Окно закрыли крестиком (стандартный путь DefWindowProcW → DestroyWindow)
                // — завершаем цикл сообщений.
                PostQuitMessage(0);
                return 0;

            case WM.WM_KEYDOWN:
                // Выход по Esc — как в остальных болванках.
                if (wParam.Value == (nuint)VK.VK_ESCAPE)
                {
                    _ = DestroyWindow(hWnd);
                }
                return 0;

            default:
                return DefWindowProcW(hWnd, message, wParam, lParam);
        }
    }
}
