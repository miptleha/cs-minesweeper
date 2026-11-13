using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MinesweeperCore.Abstractions;
using MinesweeperCore.Engine;
using MinesweeperCore.Infrastructure;
using MinesweeperCore.Domain;
using MinesweeperBlazorHybrid.Services;

namespace MinesweeperBlazorHybrid;

/// <summary>
/// Главное окно приложения — хост для BlazorWebView.
/// Настраивает DI-контейнер и запускает Blazor-приложение.
/// </summary>
public partial class MainForm : Form
{
    /// <summary>
    /// Делегат для закрытия приложения из Blazor.
    /// </summary>
    public Func<Task>? OnExitRequested { get; set; }

    /// <summary>
    /// Флаг: подписка на SizeRequested уже выполнена (защита от двойной подписки).
    /// </summary>
    private bool _isSizePending;

    /// <summary>
    /// Обработчик HandleCreated — для отписки после срабатывания (защита от утечки).
    /// </summary>
    private EventHandler? _handleCreatedHandler;

    /// <summary>
    /// true если уже подписаны на HandleCreated (защита от дублирования).
    /// </summary>
    private bool _handleCreatedSubscribed;

    /// <summary>
    /// Храним запрошенный логический размер (в CSS-пикселях)
    /// </summary>
    private Size _logicalSize = Size.Empty;

    public MainForm()
    {
        InitializeComponent();

        // Используем иконку из исполняемого файла — не требует внешних файлов.
        this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                   ?? SystemIcons.Application;
        this.Text = "Сапёр";

        // Создаём DI-контейнер: JsonRecordStore для рекордов + GameEngine для игровой логики.
        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();

        // Создаём один общий экземпляр хранилища рекордов.
        var recordStore = new JsonRecordStore();

        // Регистрируем его и как конкретный класс (нужно для @inject в Main.razor),
        // и как интерфейс (нужно для GameEngine).
        services.AddSingleton<JsonRecordStore>(recordStore);
        services.AddSingleton<IRecordStore>(recordStore);

        // Регистрируем движок, который получает интерфейс из контейнера.
        services.AddSingleton<GameEngine>(sp => new GameEngine(
            new SystemRandomProvider(),
            sp.GetRequiredService<IRecordStore>()));

        // Регистрируем делегат для закрытия приложения из Blazor.
        // Так как Blazor не может напрямую закрыть WinForms-окно, мы передаем ему Action.
        services.AddSingleton<Func<Task>>(() =>
        {
            if (InvokeRequired)
                BeginInvoke(Close);
            else
                Close();
            return Task.CompletedTask;
        });

        services.AddSingleton<DialogService>();

        // Сервис для подгонки окна под размер поля.
        var sizeService = new WindowSizeService();
        services.AddSingleton(sizeService);

        sizeService.SizeRequested += (w, h) =>
        {
            // Сохраняем логический размер (w, h + 72)
            _logicalSize = new Size(w, h + 72);

            // Защита: если размер уже был применён — игнорируем повторные запросы
            if (_isSizePending)
                return;

            _isSizePending = true;

            void Apply()
            {
                // Группа 13: умножаем логический размер на коэффициент DPI,
                // чтобы окно соответствовало отмасштабированному контенту WebView2.
                float scale = DeviceDpi / 96f;
                ClientSize = new Size((int)(_logicalSize.Width * scale), (int)(_logicalSize.Height * scale));
                _isSizePending = false;
            }

            if (!IsHandleCreated)
            {
                // Защита от множественной подписки: отписываемся перед добавлением.
                if (_handleCreatedSubscribed)
                    HandleCreated -= _handleCreatedHandler!;
                _handleCreatedHandler = (_, _) =>
                {
                    HandleCreated -= _handleCreatedHandler!;
                    _handleCreatedSubscribed = false;
                    Apply();
                };
                HandleCreated += _handleCreatedHandler!;
                _handleCreatedSubscribed = true;
                return;
            }
            if (InvokeRequired) BeginInvoke(Apply);
            else Apply();
        };

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        var provider = services.BuildServiceProvider();

        var blazorWebView = new BlazorWebView()
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html",
            Services = provider,
        };

        blazorWebView.BlazorWebViewInitialized += (s, e) =>
        {
            // Отключаем pinch-to-zoom (масштабирование двумя пальцами)
            e.WebView.CoreWebView2.Settings.IsPinchZoomEnabled = false;

            // Отключаем Ctrl + колесико мыши
            e.WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Отключаем контекстное меню "Сохранить как", "Печать" и т.д.
            e.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        };

        blazorWebView.RootComponents.Add<Components.Main>("#app");
        Controls.Add(blazorWebView);
    }
    
    /// <summary>
    /// Пересчитываем размер окна при перетаскивании на другой монитор
    /// </summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (_logicalSize != Size.Empty)
        {
            float scale = DeviceDpi / 96f;
            ClientSize = new Size((int)(_logicalSize.Width * scale), (int)(_logicalSize.Height * scale));
        }
    }
}
