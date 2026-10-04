using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;

namespace DLSS_Swapper.UserControls;

internal sealed class ManualLaunchSetupDialog : EasyContentDialog
{
    readonly List<ManualLaunchSetup.LaunchDraft> _drafts;
    readonly List<(ManualLaunchSetup.LaunchDraft Draft, ComboBox Choice)> _rows = new();
    readonly StackPanel _list = new() { Spacing = 8 };
    readonly ScrollViewer _scroll = new();
    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    readonly ExecutableTemplates _executableTemplates;
    bool _saving;

    internal ManualLaunchSetupDialog(XamlRoot root, IReadOnlyList<ManualLaunchSetup.LaunchScanEntry> scans) : base(root)
    {
        _drafts = scans.Select(scan => new ManualLaunchSetup.LaunchDraft(scan)).ToList();
        _executableTemplates = new ExecutableTemplates(Math.Clamp(root.Size.Width - 96, 200, 640));
        Title = $"Game launch setup ({_drafts.Count} games)";
        PrimaryButtonText = "Apply";
        SecondaryButtonText = "Save and close";
        CloseButtonText = "Skip and close";
        DefaultButton = ContentDialogButton.Secondary;
        Resources["ContentDialogMaxWidth"] = 800d;
        Resources["ContentDialogMinWidth"] = 0d;
        var content = new Grid { RowSpacing = 12, Height = Math.Min(440, Math.Max(180, root.Size.Height - 240)) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            content.RowDefinitions.Add(new RowDefinition { Height = height });
        content.Children.Add(new TextBlock
        {
            Text = "Confirm the launch executable for each game. Best-effort suggestion selected. Verify it is the correct game executable before saving",
            TextWrapping = TextWrapping.Wrap
        });
        var headers = MakeRow();
        headers.Children.Add(new TextBlock { Text = "Game", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var executableHeader = new TextBlock { Text = "Launch executable", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetColumn(executableHeader, 1); headers.Children.Add(executableHeader);
        Grid.SetRow(headers, 1); content.Children.Add(headers);
        foreach (var draft in _drafts) AddRow(draft);
        _scroll.Content = _list;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Grid.SetRow(_scroll, 2); content.Children.Add(_scroll);
        var statusScroll = new ScrollViewer { Content = _status, MaxHeight = 64, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(statusScroll, 3); content.Children.Add(statusScroll);
        Content = content;
        PrimaryButtonClick += async (_, e) =>
        {
            e.Cancel = true;
            if (_saving) return;
            var deferral = e.GetDeferral();
            try { await SaveAsync(); }
            finally { deferral.Complete(); }
        };
        SecondaryButtonClick += async (_, e) =>
        {
            e.Cancel = true;
            if (_saving) return;
            var deferral = e.GetDeferral();
            try { e.Cancel = !await SaveAsync(skipUnselected: true); }
            finally { deferral.Complete(); }
        };
        CloseButtonClick += (_, e) => e.Cancel = _saving;
        Closing += (_, e) => e.Cancel = _saving;
        ToolTipService.SetToolTip(this, "Right-click a game row for launch arguments and working folder.");
    }

    static Grid MakeRow()
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        return row;
    }

    void AddRow(ManualLaunchSetup.LaunchDraft draft)
    {
        var row = MakeRow();
        var name = new TextBlock { Text = draft.Game.Title, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(name, draft.Game.Title);
        var choice = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplateSelector = _executableTemplates,
            PlaceholderText = "Not found — Browse", MinWidth = 0 };
        AutomationProperties.SetName(choice, $"{draft.Game.Title} launch executable");
        void SelectPath(string path)
        {
            var candidate = draft.Candidates.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            if (candidate is null && !string.IsNullOrWhiteSpace(path))
            {
                candidate = new(path, Path.GetFileName(path));
                draft.Candidates.Add(candidate);
            }
            choice.ItemsSource = draft.Candidates.ToArray();
            choice.SelectedItem = candidate;
            ToolTipService.SetToolTip(choice, string.IsNullOrEmpty(draft.Error) ? path : draft.Error);
        }
        SelectPath(draft.Executable);
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedItem is not ManualLaunchManifest.Candidate selected) return;
            draft.Executable = selected.Path;
            draft.Error = "";
            choice.ClearValue(Control.BorderBrushProperty);
            ToolTipService.SetToolTip(choice, selected.Path);
        };
        var browse = new Button { Content = "Browse", HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(browse, $"Browse for {draft.Game.Title}");
        browse.Click += (_, _) =>
        {
            try
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);
                var path = FileSystemHelper.OpenFile(handle, new[] { new FileSystemHelper.FileFilter("Game executable", "*.exe") },
                    defaultPath: draft.Game.InstallPath);
                if (!string.IsNullOrEmpty(path))
                {
                    if (ManualLaunchManifest.IsExcluded(path)) throw new IOException("That executable is excluded from game launching.");
                    SelectPath(path);
                }
            }
            catch (Exception ex) { draft.Error = ex.Message; ShowErrors(); }
        };
        var menu = new MenuFlyout();
        var options = new MenuFlyoutItem { Text = "Launch options…" };
        options.Click += (_, _) => ShowOptions(name, draft);
        menu.Items.Add(options); row.ContextFlyout = menu;
        row.Children.Add(name);
        Grid.SetColumn(choice, 1); row.Children.Add(choice);
        Grid.SetColumn(browse, 2); row.Children.Add(browse);
        _rows.Add((draft, choice)); _list.Children.Add(row);
    }

    static void ShowOptions(FrameworkElement target, ManualLaunchSetup.LaunchDraft draft)
    {
        var arguments = new TextBox { Header = "Launch arguments (optional)", Text = draft.Arguments };
        var working = new TextBox { Header = "Working folder (optional)", Text = draft.WorkingDirectory };
        arguments.TextChanged += (_, _) => draft.Arguments = arguments.Text;
        working.TextChanged += (_, _) => draft.WorkingDirectory = working.Text;
        var content = new StackPanel { Spacing = 12, Width = 360 };
        content.Children.Add(arguments); content.Children.Add(working);
        new Flyout { Content = content }.ShowAt(target);
    }

    // WinUI requests the dropdown template with a ComboBoxItem container and
    // the collapsed selection template with its presenter (or no container).
    sealed class ExecutableTemplates : DataTemplateSelector
    {
        readonly DataTemplate _fileName;
        readonly DataTemplate _fullPath;

        internal ExecutableTemplates(double pathWidth)
        {
            _fileName = (DataTemplate)XamlReader.Load("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <TextBlock Text="{Binding FileName}" TextWrapping="Wrap" FlowDirection="LeftToRight" />
                </DataTemplate>
                """);
            _fullPath = (DataTemplate)XamlReader.Load(FormattableString.Invariant($$"""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <TextBlock Text="{Binding Path}" TextWrapping="Wrap" MaxWidth="{{pathWidth}}"
                               FlowDirection="LeftToRight" ToolTipService.ToolTip="{Binding Path}" />
                </DataTemplate>
                """));
        }

        protected override DataTemplate SelectTemplateCore(object item) => _fileName;
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
            container is ComboBoxItem ? _fullPath : _fileName;
    }

    async Task<bool> SaveAsync(bool skipUnselected = false)
    {
        _saving = true;
        IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = _scroll.IsEnabled = false;
        _status.Text = "Saving…";
        try
        {
            var failures = await ManualLaunchSetup.SaveDraftsAsync(_drafts, skipUnselected);
            ShowErrors();
            if (failures == 0) _status.Text = "Launch settings saved.";
            return failures == 0;
        }
        finally
        {
            _saving = false;
            IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = _scroll.IsEnabled = true;
        }
    }

    void ShowErrors()
    {
        _status.Text = string.Join("\n", _drafts.Where(draft => draft.Error.Length > 0).Select(draft => $"{draft.Game.Title}: {draft.Error}"));
        foreach (var (draft, choice) in _rows)
        {
            if (draft.Error.Length > 0) choice.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            else choice.ClearValue(Control.BorderBrushProperty);
            ToolTipService.SetToolTip(choice, draft.Error.Length > 0 ? draft.Error : draft.Executable);
        }
    }
}
