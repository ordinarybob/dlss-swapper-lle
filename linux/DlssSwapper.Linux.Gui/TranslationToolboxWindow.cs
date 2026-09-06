using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class TranslationToolboxWindow : Window
{
    private readonly TranslationDocument _document = new(new Translations("en-US"));
    private readonly ListBox _keys = new();
    private readonly TextBox _editor = new() { Name = "TranslationEditor", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140 };
    private readonly TextBlock _source = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _comment = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _language = new() { ItemsSource = Translations.Languages, SelectedItem = "en-US" };
    private bool _dirty, _updating, _busy, _closeApproved;

    public TranslationToolboxWindow(string savedLanguage)
    {
        string Text(string key, string fallback) => LanguageAppearance.Get(key, fallback);
        Title = Text("TranslationToolboxPage_WindowTitle", "Translation Toolbox");
        Width = 950; Height = 700; MinWidth = 650; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto,Auto"), Margin = new Thickness(18), RowSpacing = 10 };
        var heading = new StackPanel { Spacing = 6 };
        heading.Children.Add(new TextBlock { Text = Text("TranslationToolboxPage_SourceLanguage", "Source language") });
        heading.Children.Add(_language); layout.Children.Add(heading);
        var body = new Grid { ColumnDefinitions = new("2*,3*"), ColumnSpacing = 12 };
        body.Children.Add(_keys);
        var edit = new StackPanel { Spacing = 8 };
        edit.Children.Add(new TextBlock { Text = Text("TranslationToolboxPage_SourceTranslation", "Source translation") });
        edit.Children.Add(_source);
        edit.Children.Add(_comment);
        edit.Children.Add(new TextBlock { Text = Text("TranslationToolboxPage_NewTranslation", "New translation") });
        edit.Children.Add(_editor);
        var scroll = new ScrollViewer { Content = edit };
        Grid.SetColumn(scroll, 1); body.Children.Add(scroll); Grid.SetRow(body, 1); layout.Children.Add(body);
        Grid.SetRow(_status, 2); layout.Children.Add(_status);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        Grid.SetRow(actions, 3); layout.Children.Add(actions); Content = layout;
        void Action(string key, string label, Func<Task> run)
        {
            var button = new Button { Content = Text(key, label), Margin = new Thickness(0, 0, 8, 8) };
            actions.Children.Add(button);
            button.Click += async (_, _) =>
            {
                if (_busy) return;
                _busy = true; actions.IsEnabled = false; body.IsEnabled = false; heading.IsEnabled = false;
                try { await run(); }
                catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _status.Text = ex.Message; }
                finally { _busy = false; actions.IsEnabled = true; body.IsEnabled = true; heading.IsEnabled = true; }
            };
        }
        Action("General_Load", "Load", async () =>
        {
            if (!await MayDiscardAsync()) return;
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Title, FileTypeFilter = [new(LanguageAppearance.Get("Linux_FileTypeTranslations", "Translation files")) { Patterns = ["*.json", "*.csv"] }]
            });
            if (files.Count == 0) return;
            _document.Load(files[0].TryGetLocalPath() ?? throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingLocalFile", "Choose a local file.")));
            BindKeys(); _dirty = false;
            _status.Text = Text("Linux_TranslationLoaded", "Translations loaded.");
        });
        Action("TranslationToolboxPage_LoadExistingTranslation", "Load existing translation", async () =>
        {
            if (!await MayDiscardAsync()) return;
            var folder = Path.Combine(AppContext.BaseDirectory, "Translations", (string)_language.SelectedItem!);
            var values = Translations.Read(Path.Combine(folder, "Resources.resw"));
            var supplement = Path.Combine(folder, "Linux.resw");
            if (File.Exists(supplement)) foreach (var pair in Translations.Read(supplement)) values[pair.Key] = pair.Value;
            foreach (var key in _document.Rows.Keys.ToArray())
                _document.Rows[key] = _document.Rows[key] with { NewTranslation = values.GetValueOrDefault(key, "") };
            _dirty = true; UpdateEditor();
            _status.Text = Text("Linux_TranslationLoaded", "Translations loaded.");
        });
        Action("General_Save", "Save", async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Title, SuggestedFileName = "DLSS-Swapper-translations.json", DefaultExtension = "json",
                FileTypeChoices = [new("JSON") { Patterns = ["*.json"] }, new("CSV") { Patterns = ["*.csv"] }]
            });
            if (file is null) return;
            await _document.SaveAsync(file.TryGetLocalPath() ?? throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingLocalFile", "Choose a local file.")));
            _dirty = false;
            _status.Text = Text("Linux_TranslationSaved", "Translations saved.");
        });
        Action("TranslationToolboxPage_ReloadApp", "Reload preview", () =>
        {
            LanguageAppearance.Apply((string)_language.SelectedItem!, _document.Edits());
            _status.Text = Text("Linux_TranslationPreview", "Preview applied. Closing the toolbox restores your saved language.");
            return Task.CompletedTask;
        });
        Action("General_Publish", "Publish", async () =>
        {
            if (_document.Edits().Count == 0) throw new IOException(Text("TranslationToolboxPage_NoDataToPublish", "There are no translations to export."));
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Title, SuggestedFileName = "DLSS-Swapper-translations.zip", DefaultExtension = "zip",
                FileTypeChoices = [new("ZIP") { Patterns = ["*.zip"] }]
            });
            if (file is null) return;
            await _document.PublishAsync(file.TryGetLocalPath() ?? throw new IOException(LanguageAppearance.Get("Linux_GuiRemainingLocalFile", "Choose a local file.")));
            _status.Text = Text("Linux_TranslationExported", "ZIP saved locally. Nothing was uploaded.");
        });
        Action("General_Close", "Close", async () =>
        {
            if (await MayDiscardAsync()) { _closeApproved = true; Close(); }
        });
        _language.SelectionChanged += (_, _) =>
        {
            var source = new Translations((string)_language.SelectedItem!);
            foreach (var key in _document.Rows.Keys.ToArray())
                _document.Rows[key] = _document.Rows[key] with { SourceTranslation = source.Get(key, "") };
            UpdateEditor();
        };
        _keys.SelectionChanged += (_, _) => UpdateEditor();
        _editor.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty || _updating || _keys.SelectedItem is not string key) return;
            if (_document.Rows[key].NewTranslation == (_editor.Text ?? "")) return;
            _document.Rows[key] = _document.Rows[key] with { NewTranslation = _editor.Text ?? "" };
            _dirty = true;
        };
        Closing += async (_, e) =>
        {
            if (_closeApproved) return;
            if (_busy) { e.Cancel = true; return; }
            if (!_dirty) return;
            e.Cancel = true;
            _busy = true;
            try { if (await MayDiscardAsync()) { _closeApproved = true; Close(); } }
            finally { _busy = false; }
        };
        Closed += (_, _) => LanguageAppearance.Apply(savedLanguage);
        BindKeys();
    }

    private void BindKeys()
    {
        _keys.ItemsSource = _document.Rows.Keys.Order(StringComparer.Ordinal).ToArray();
        _keys.SelectedIndex = 0;
    }

    private void UpdateEditor()
    {
        _updating = true;
        try
        {
            if (_keys.SelectedItem is not string key) return;
            var row = _document.Rows[key];
            _source.Text = row.SourceTranslation;
            _comment.Text = row.Comment;
            _editor.Text = row.NewTranslation;
        }
        finally { _updating = false; }
    }

    private async Task<bool> MayDiscardAsync() => !_dirty || await new ConfirmationDialog(
        LanguageAppearance.Get("TranslationToolboxPage_UnsavedChangesTitle", "Unsaved changes"),
        LanguageAppearance.Get("Linux_TranslationDiscard", "Continue and discard unsaved translation changes?")).ShowDialog<bool>(this);
}
