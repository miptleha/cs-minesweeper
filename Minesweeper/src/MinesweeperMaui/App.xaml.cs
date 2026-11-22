using Microsoft.Extensions.DependencyInjection;

namespace MinesweeperMaui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Окно болванки: заголовок и иконка в заголовке (Windows)
		var window = new Window(new AppShell())
		{
			Title = "Сапёр",
			Width = 800,
			Height = 600
		};

		// Иконка заголовка: PNG из MauiImage-ресурсов (см. csproj, MauiImage Include)
		window.TitleBar = new TitleBar
		{
			Icon = "bomb.png",
			Title = "Сапёр"
		};

		return window;
	}
}