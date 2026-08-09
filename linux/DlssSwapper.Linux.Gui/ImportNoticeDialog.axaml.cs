using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DlssSwapper.Linux.Gui;

public sealed record ImportNoticeResult(bool Proceed, bool DontShowAgain);

public sealed partial class ImportNoticeDialog : Window
{
    private CheckBox? _dontShowAgain;

    public ImportNoticeDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public ImportNoticeDialog(string body, string action)
        : this()
    {
        Title = string.Empty;
        FindRequired<TextBlock>("BodyTextBlock").Text = body;
        FindRequired<Button>("ProceedButton").Content = action;
        _dontShowAgain = FindRequired<CheckBox>("DontShowAgainCheckBox");
    }

    private void Proceed_Click(object? sender, RoutedEventArgs e) =>
        Close(new ImportNoticeResult(true, _dontShowAgain?.IsChecked == true));

    private void Cancel_Click(object? sender, RoutedEventArgs e) =>
        Close(new ImportNoticeResult(false, false));

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Required import-notice control '{name}' is missing.");
}
