using System.Windows;
using System.Windows.Navigation;

namespace MinesweeperWpf;

/// <summary>
/// Interaction logic for AboutWindow.xaml
/// Окно «О программе».
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Обработка клика по гиперссылке (открывает браузер).
    /// </summary>
    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
