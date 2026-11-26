using Avalonia.Controls;

namespace MinesweeperAtomUi;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // DoD: минимум один ассет реально загружается
        try
        {
            _ = Assets.GetPng("smiley");
        }
        catch
        {
            // ассет не критичен для работы окна
        }
    }
}