using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class ManualGameImportDialog : Window
{
    private readonly TextBox _name;
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Image _preview = new() { Stretch = Stretch.UniformToFill };
    private readonly Button _coverButton = new() { Content = LanguageAppearance.Get("Linux_ManualGameImportDialog_103", "Add cover (optional)"), Width = 200, Height = 300 };
    private readonly CancellationTokenSource _lifetime = new();
    private byte[]? _cover;
    private Bitmap? _bitmap;
    private bool _saving;
    private bool _closeRequested;

    public ManualGameImportDialog(PersistentLibrary library, string root, Func<IEnumerable<string>> existingPaths)
    {
        Title = LanguageAppearance.Get("Linux_ManualGameImportDialog_104", "Add game");
        Width = 700; Height = 480; MinWidth = 600; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { Margin = new Thickness(20), RowDefinitions = new("*,Auto,Auto"), RowSpacing = 12 };
        var editor = new Grid { ColumnDefinitions = new("200,*"), ColumnSpacing = 20 };
        editor.Children.Add(_coverButton);
        _name = new TextBox { Text = Path.GetFileName(root) };
        var fields = new StackPanel { Spacing = 8 };
        fields.Children.Add(new TextBlock { [!TextBlock.TextProperty] = new DynamicResourceExtension("General_Name"), FontWeight = FontWeight.Bold });
        fields.Children.Add(_name);
        fields.Children.Add(new TextBlock { [!TextBlock.TextProperty] = new DynamicResourceExtension("GamePage_InstallPath"), FontWeight = FontWeight.Bold, Margin = new Thickness(0,12,0,0) });
        fields.Children.Add(new TextBox { Text = root, IsReadOnly = true, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(fields, 1); editor.Children.Add(fields); layout.Children.Add(editor);
        Grid.SetRow(_error, 1); layout.Children.Add(_error);
        var actions = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 10 };
        var save = new Button { Content = LanguageAppearance.Get("Linux_ManualGameImportDialog_104", "Add game"), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        save.Classes.Add("primary");
        var cancel = new Button { [!ContentControl.ContentProperty] = new DynamicResourceExtension("General_Cancel"), IsCancel = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        actions.Children.Add(save); Grid.SetColumn(cancel,1); actions.Children.Add(cancel);
        Grid.SetRow(actions, 2); layout.Children.Add(actions); Content = layout;
        _coverButton.Click += async (_, _) =>
        {
            if (_saving) return;
            if (_cover is not null)
            {
                if (await new ConfirmationDialog(LanguageAppearance.Get("Linux_GuiRemainingRemoveCover", "Remove cover"), LanguageAppearance.Get("Linux_GuiRemainingRemoveCoverPrompt", "Remove this custom cover?")).ShowDialog<bool>(this))
                { _cover = null; _preview.Source = null; _bitmap?.Dispose(); _bitmap = null; _coverButton.Content = LanguageAppearance.Get("Linux_ManualGameImportDialog_103", "Add cover (optional)"); }
                return;
            }
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = LanguageAppearance.Get("Linux_ManualGameImportDialog_102", "Select cover art"), AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType(LanguageAppearance.Get("Linux_FileTypeImages", "Image files")) { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }]
                });
                if (!_lifetime.IsCancellationRequested && files.Count > 0)
                    SetCover(files.Select(file => file.TryGetLocalPath()).ToArray());
            }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _error.Text = ex.Message; }
        };
        DragDrop.SetAllowDrop(_coverButton, true);
        _coverButton.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.Handled = true; e.DragEffects = DragDropEffects.None;
            if (_saving) return;
            try { CustomCoverWorkflow.ValidateSelection(e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).ToArray() ?? []); e.DragEffects = DragDropEffects.Copy; }
            catch (IOException) { }
        });
        _coverButton.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            if (_saving) return;
            try { SetCover(e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).ToArray() ?? []); }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _error.Text = ex.Message; }
        });
        save.Click += async (_, _) =>
        {
            if (_saving) return;
            _saving = true; editor.IsEnabled = false; save.IsEnabled = false;
            try
            {
                var game = await ManualGameImportWorkflow.SaveAsync(library, root, _name.Text ?? "", _cover,
                    new AvaloniaArtworkImageProcessor(), existingPaths, _lifetime.Token);
                _saving = false; Close(game);
            }
            catch (OperationCanceledException) { _error.Text = LanguageAppearance.Get("Linux_ManualGameImportDialog_101", "Adding the game was cancelled."); }
            catch (Exception ex) { AppLog.Write(ApplicationLogLevel.Error, ex.Message); _error.Text = LanguageAppearance.Format("Linux_ManualGameImportDialog_100", "Could not add game: {0} Your choices are still here; retry or cancel.", ex.Message); }
            finally
            {
                _saving = false; editor.IsEnabled = true; save.IsEnabled = true;
                if (_closeRequested) Close();
            }
        };
        cancel.Click += (_, _) => Close();
        Closing += (_, e) =>
        {
            _closeRequested = true;
            if (_saving) { e.Cancel = true; _lifetime.Cancel(); }
        };
        Closed += (_, _) => { _lifetime.Cancel(); _bitmap?.Dispose(); _lifetime.Dispose(); };
    }

    private void SetCover(IReadOnlyList<string?> paths)
    {
        var source = CustomCoverWorkflow.ValidateSelection(paths);
        var bytes = File.ReadAllBytes(source);
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new Bitmap(stream);
        _preview.Source = bitmap; _bitmap?.Dispose(); _bitmap = bitmap; _cover = bytes;
        _coverButton.Content = _preview; _error.Text = "";
    }
}
