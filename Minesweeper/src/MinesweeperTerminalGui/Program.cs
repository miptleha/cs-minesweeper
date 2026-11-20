using Terminal.Gui.App;           // Application, IApplication, MessageBox
using Terminal.Gui.Configuration; // ConfigurationManager
using Terminal.Gui.ViewBase;      // View, Pos, Dim
using Terminal.Gui.Views;         // Window, Label, Button, ...

ThemeManager.Theme = "TurboPascal 5";

// INSTANCE lifecycle — not static Init/Run/Shutdown:
Application.Create ().Run<MainWindow> ().Dispose ();

internal sealed class MainWindow : Window   // bordered root; use `Runnable` for borderless
{
    public MainWindow ()
    {
        Title = "Сапёр (Esc to quit)";
        Button b = new () { Text = "_OK", X = Pos.Center (), Y = Pos.Center () };
        b.Accepted += (_, _) => App!.RequestStop ();
        Add (b);
    }
}