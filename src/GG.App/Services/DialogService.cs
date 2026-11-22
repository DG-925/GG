using System.IO;
using System.Windows;
using GG.App.Views;
using Microsoft.Win32;

namespace GG.App.Services;

/// <summary>What the user chose in the launch-error dialog.</summary>
public enum LaunchErrorChoice
{
    Close,
    LocateExe,
}

/// <summary>File/folder pickers and GG's own dialogs, kept out of the view models.</summary>
public interface IDialogService
{
    string? PickFolder(string title, string? initialFolder = null);

    string? PickExe(string title, string? initialFolder = null);

    LaunchErrorChoice ShowLaunchError(string gameName, string message, bool canLocate);
}

public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public string? PickFolder(string title, string? initialFolder = null)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            dialog.InitialDirectory = initialFolder;
        return Show(dialog) ? dialog.FolderName : null;
    }

    public string? PickExe(string title, string? initialFolder = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            dialog.InitialDirectory = initialFolder;
        return Show(dialog) ? dialog.FileName : null;
    }

    private static bool Show(CommonDialog dialog) =>
        (Owner is { IsVisible: true } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog()) == true;

    public LaunchErrorChoice ShowLaunchError(string gameName, string message, bool canLocate)
    {
        var dialog = new LaunchErrorDialog(gameName, message, canLocate);
        if (Owner is { IsVisible: true } owner)
            dialog.Owner = owner;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        return dialog.Choice;
    }
}
