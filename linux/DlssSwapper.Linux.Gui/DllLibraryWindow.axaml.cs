using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui.ViewModels;

namespace DlssSwapper.Linux.Gui;

public sealed partial class DllLibraryWindow : Window
{
    private readonly ComboBox _familyComboBox;
    private readonly TextBox _searchTextBox;
    private readonly ListBox _entryListBox;
    private readonly TextBlock _statusText;
    private readonly Button[] _actionButtons;
    private readonly DownloadCache _cache = new();
    private DllCatalog? _catalog;
    private IReadOnlyList<DllLibraryEntryViewModel> _allEntries = [];

    public DllLibraryWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _familyComboBox = FindRequired<ComboBox>("FamilyComboBox");
        _searchTextBox = FindRequired<TextBox>("SearchTextBox");
        _entryListBox = FindRequired<ListBox>("EntryListBox");
        _statusText = FindRequired<TextBlock>("StatusText");
        _actionButtons =
        [
            FindRequired<Button>("DownloadButton"),
            FindRequired<Button>("RemoveButton"),
            FindRequired<Button>("DownloadLatestButton"),
            FindRequired<Button>("UseButton"),
        ];
        Closed += (_, _) => _cache.Dispose();
    }

    public DllLibraryWindow(DllCatalog catalog)
        : this()
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _familyComboBox.ItemsSource = new[] { "All families" }
            .Concat(DllTypes.All.Select(definition => definition.DisplayName))
            .ToArray();
        _familyComboBox.SelectedIndex = 0;
        _allEntries = catalog.GetEntries()
            .Select(entry => new DllLibraryEntryViewModel(entry, _cache.IsCached(entry)))
            .ToArray();
        ApplyFilter();
        _statusText.Text = $"{_allEntries.Count:N0} verified release build{Plural(_allEntries.Count)} available.";
    }

    private void Filter_Changed(object? sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Search_Changed(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelected(out var row))
        {
            return;
        }

        await DownloadAsync(row);
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelected(out var row))
        {
            return;
        }

        try
        {
            var removed = _cache.Remove(row.Entry);
            row.IsCached = false;
            _statusText.Text = removed
                ? $"Removed {row.Family} {row.Version} ({row.Build}) from the shared cache."
                : "That release is not downloaded.";
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Could not remove the cached release: {exception.Message}";
        }
    }

    private async void DownloadLatest_Click(object? sender, RoutedEventArgs e)
    {
        if (_catalog is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var latest = DllTypes.All.Select(definition => _catalog.GetLatest(definition.Type))
                .ToArray();
            var completed = 0;
            foreach (var entry in latest)
            {
                _statusText.Text = $"Downloading latest verified families: {completed} / {latest.Length}…";
                await _cache.GetAsync(entry, CancellationToken.None);
                var row = _allEntries.First(item => ReferenceEquals(item.Entry, entry));
                row.IsCached = true;
                completed++;
            }

            _statusText.Text = $"Downloaded or verified {completed} latest family release{Plural(completed)}.";
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Latest-family download stopped: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Use_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetSelected(out var row))
        {
            return;
        }

        if (!row.IsCached && !await DownloadAsync(row))
        {
            return;
        }

        Close(row.Entry);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void ApplyFilter()
    {
        if (_entryListBox is null)
        {
            return;
        }

        IEnumerable<DllLibraryEntryViewModel> entries = _allEntries;
        var familyIndex = _familyComboBox.SelectedIndex;
        if (familyIndex > 0 && familyIndex <= DllTypes.All.Count)
        {
            var type = DllTypes.All[familyIndex - 1].Type;
            entries = entries.Where(row => row.Entry.Type == type);
        }

        var search = _searchTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            entries = entries.Where(row => row.Version.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Entry.Md5.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _entryListBox.ItemsSource = entries.ToArray();
    }

    private async Task<bool> DownloadAsync(DllLibraryEntryViewModel row)
    {
        SetBusy(true);
        _statusText.Text = $"Downloading and verifying {row.Family} {row.Version} ({row.Build})…";
        try
        {
            await _cache.GetAsync(row.Entry, CancellationToken.None);
            row.IsCached = true;
            _statusText.Text = $"{row.Family} {row.Version} ({row.Build}) is ready in the shared cache.";
            return true;
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Download failed: {exception.Message}";
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool TryGetSelected(out DllLibraryEntryViewModel row)
    {
        row = _entryListBox.SelectedItem as DllLibraryEntryViewModel ?? null!;
        if (row is not null)
        {
            return true;
        }

        _statusText.Text = "Select one release first.";
        return false;
    }

    private void SetBusy(bool busy)
    {
        foreach (var button in _actionButtons)
        {
            button.IsEnabled = !busy;
        }
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Required control '{name}' is missing.");

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
