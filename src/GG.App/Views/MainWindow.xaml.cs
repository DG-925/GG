using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GG.App.ViewModels;

namespace GG.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Ctrl+F jumps to search, Esc clears it.
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin && DataContext is MainViewModel vm)
        {
            vm.ClearSearchCommand.Execute(null);
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }
}

/// <summary>Asks Windows 10 (1809+) / 11 to draw the native title bar dark to match the app.</summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkModeOld = 19; // Windows 10 before 20H1
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;            // Windows 11 only

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var on = 1;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeOld, ref on, sizeof(int));

            // COLORREF is 0x00BBGGRR; match the window background #0E1014.
            var caption = 0x0014100E;
            DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
        }
        catch
        {
            // Purely cosmetic; ignore on older systems.
        }
    }
}
