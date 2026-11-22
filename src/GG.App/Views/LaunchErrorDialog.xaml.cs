using System.Windows;
using GG.App.Services;

namespace GG.App.Views;

/// <summary>"Couldn't start X" with a "Locate exe..." button.</summary>
public partial class LaunchErrorDialog : Window
{
    public LaunchErrorDialog(string gameName, string message, bool canLocate)
    {
        InitializeComponent();
        TitleText.Text = $"Couldn't start {gameName}";
        MessageText.Text = message;
        if (!canLocate)
            LocateButton.Visibility = Visibility.Collapsed;
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }

    public LaunchErrorChoice Choice { get; private set; } = LaunchErrorChoice.Close;

    private void OnLocate(object sender, RoutedEventArgs e)
    {
        Choice = LaunchErrorChoice.LocateExe;
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
