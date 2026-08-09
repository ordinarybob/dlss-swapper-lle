using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed partial class SettingsWindow : Window
{
    private PersistentLibrary? _library;
    private readonly CheckBox _hddMode;
    private readonly NumericUpDown _gridColumns;
    private readonly NumericUpDown _gridRows;
    private readonly TextBox _mediaWikiEndpoint;
    private readonly TextBox _mediaWikiHost;
    private readonly TextBox _customPatterns;
    private readonly TextBox _additionalSteamRoots;
    private readonly TextBlock _validation;

    public SettingsWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _hddMode = FindRequired<CheckBox>("HddModeCheckBox");
        _gridColumns = FindRequired<NumericUpDown>("GridColumnsInput");
        _gridRows = FindRequired<NumericUpDown>("GridRowsInput");
        _mediaWikiEndpoint = FindRequired<TextBox>("MediaWikiEndpointTextBox");
        _mediaWikiHost = FindRequired<TextBox>("MediaWikiHostTextBox");
        _customPatterns = FindRequired<TextBox>("CustomPatternsTextBox");
        _additionalSteamRoots = FindRequired<TextBox>("AdditionalSteamRootsTextBox");
        _validation = FindRequired<TextBlock>("ValidationTextBlock");
    }

    public SettingsWindow(PersistentLibrary library)
        : this()
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));

        _hddMode.IsChecked = library.State.HddMode;
        _gridColumns.Value = library.State.GridColumns;
        _gridRows.Value = library.State.GridRows;
        _mediaWikiEndpoint.Text = library.State.MediaWikiApiEndpoint;
        _mediaWikiHost.Text = library.State.MediaWikiImageHost;
        FindRequired<TextBox>("BuiltInPatternsTextBox").Text = string.Join(
            Environment.NewLine,
            FastScanPatternIndex.BuiltInPatterns.Select(DisplayPattern));
        _customPatterns.Text = string.Join(
            Environment.NewLine,
            library.State.CustomScanPatterns.Select(DisplayPattern));
        _additionalSteamRoots.Text = string.Join(
            Environment.NewLine,
            library.State.AdditionalSteamRoots);
    }

    private void ResetArtworkSource_Click(object? sender, RoutedEventArgs e)
    {
        _mediaWikiEndpoint.Text = LinuxLibraryState.DefaultMediaWikiApiEndpoint;
        _mediaWikiHost.Text = LinuxLibraryState.DefaultMediaWikiImageHost;
        _validation.Text = string.Empty;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_library is null)
        {
            _validation.Text = "The persistent library is unavailable.";
            return;
        }

        if (!LibraryStateStore.TryValidateArtworkSource(
            _mediaWikiEndpoint.Text,
            _mediaWikiHost.Text,
            out var endpoint,
            out var host,
            out var error))
        {
            _validation.Text = error;
            return;
        }

        _library.State.HddMode = _hddMode.IsChecked == true;
        _library.State.GridColumns = Decimal.ToInt32(_gridColumns.Value ?? 6);
        _library.State.GridRows = Decimal.ToInt32(_gridRows.Value ?? 5);
        _library.State.MediaWikiApiEndpoint = endpoint;
        _library.State.MediaWikiImageHost = host;
        _library.State.CustomScanPatterns = ParseLines(_customPatterns.Text).ToList();
        _library.State.AdditionalSteamRoots = ParseLines(_additionalSteamRoots.Text).ToList();
        _library.Save();
        Close(true);
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Required settings control '{name}' is missing.");

    private static IReadOnlyList<string> ParseLines(string? value) =>
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static string DisplayPattern(string pattern) =>
        pattern.Length == 0 ? "(game folder root)" : pattern;
}
