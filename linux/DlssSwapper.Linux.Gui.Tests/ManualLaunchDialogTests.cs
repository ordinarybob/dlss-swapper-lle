using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DlssSwapper.Linux.Cli.Core;
using DlssSwapper.Linux.Gui;

namespace DlssSwapper.Linux.Gui.Tests;

internal static class ManualLaunchDialogTests
{
    internal static void Run(Window owner, string fixtureRoot, string? screenshot = null)
    {
        var root = Path.Combine(fixtureRoot, "Citron"); Directory.CreateDirectory(root);
        foreach (var name in new[] { "citron.exe", "citron-cmd.exe", "GameHelper.exe", "setup.exe" })
            File.WriteAllBytes(Path.Combine(root, name), [1,2,3]);
        Directory.CreateDirectory(Path.Combine(root, "Artbook"));
        File.WriteAllBytes(Path.Combine(root, "Artbook", "citron.exe"), [1,2,3]);
        var nestedFolder = Path.Combine(root, "Engine", "Binaries", "Win64");
        Directory.CreateDirectory(nestedFolder);
        var nestedExecutable = Path.Combine(nestedFolder, "citron.exe");
        File.WriteAllBytes(nestedExecutable, [1,2,3]);
        var library = new PersistentLibrary(new LibraryStateStore(Path.Combine(fixtureRoot, "launch-state")));
        library.AddManualGames([root]);
        var games = library.State.ManualGames.ToArray();
        var offered = ManualLaunchSetupWindow.OfferAsync(owner, library, games);
        var prompt = owner.OwnedWindows.Single();
        CheckActionsAtMinimum(prompt, "Yes", "No");
        Check(prompt.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("best-effort") == true), "Offer omitted verification guidance");
        prompt.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
        using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Click(prompt, "No"); Dispatcher.UIThread.RunJobs();
            Check(!offered.IsCompleted && owner.OwnedWindows.Contains(prompt), "Preference save failure closed the offer");
            Check(prompt.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Could not save your preference:") == true), "Preference save failure hid its error");
            Check(!library.State.DontShowManualLaunchPrompt, "Preference save failure changed remembered state");
            CheckActionsAtMinimum(prompt, "Yes", "No");
            Check(prompt.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked == true, "Preference save failure discarded the user's choice");
        }
        Click(prompt, "No"); Until(() => offered.IsCompleted); offered.GetAwaiter().GetResult();
        Check(library.State.DontShowManualLaunchPrompt && !library.State.SetupManualLaunchOnImport && library.State.ManualGames.Single().Launch is null, "Remembered No did not skip launch setup");
        offered = ManualLaunchSetupWindow.OfferAsync(owner, library, games);
        Check(offered.IsCompletedSuccessfully && owner.OwnedWindows.Count == 0, "Remembered No showed another prompt");
        library.UpdateState(state => state.DontShowManualLaunchPrompt = false);
        offered = ManualLaunchSetupWindow.OfferAsync(owner, library, games);
        prompt = owner.OwnedWindows.Single(); prompt.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true; Click(prompt, "Yes");
        Until(() => owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Any());
        var setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Until(() => Button(setup, "Apply").IsEnabled);
        var choice = Choice(setup, root);
        Check(((DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.Candidate)choice.SelectedItem!).Path == Path.Combine(root, "citron.exe"), "Wrong suggestion");
        var nested = choice.Items.Cast<DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.Candidate>().Single(item => item.Path == nestedExecutable);
        var selected = choice.SelectedItem;
        choice.SelectedItem = nested; Dispatcher.UIThread.RunJobs();
        Check(choice.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "citron.exe"), "Collapsed selection did not show the complete filename");
        choice.IsDropDownOpen = true; Dispatcher.UIThread.RunJobs();
        var popup = choice.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Single();
        var popupContent = popup.Child ?? throw new Exception("Dropdown has no content");
        Check(popupContent.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == nestedExecutable && text.TextWrapping == Avalonia.Media.TextWrapping.Wrap),
            "Expanded choices must show full paths and wrap long paths");
        if (screenshot is not null)
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize((int)Math.Ceiling(popupContent.Bounds.Width), (int)Math.Ceiling(popupContent.Bounds.Height)), new Vector(96, 96));
            bitmap.Render(popupContent); bitmap.Save(screenshot + ".dropdown.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        choice.IsDropDownOpen = false; Dispatcher.UIThread.RunJobs();
        Check(ReferenceEquals(choice.SelectedItem, nested), "Opening the dropdown changed the selected path");
        choice.SelectedItem = selected;
        CheckActionsAtMinimum(setup, "Apply", "Save and close", "Skip and close");
        Check(choice.Items.Cast<DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.Candidate>().All(item =>
            !item.Path.Contains("Artbook") && item.FileName != "GameHelper.exe" && item.FileName != "setup.exe"), "Excluded executable suggested");
        var row = choice.GetVisualAncestors().OfType<Grid>().First();
        ((MenuItem)row.ContextMenu!.Items.Single()!).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var options = setup.OwnedWindows.Single();
        options.GetVisualDescendants().OfType<TextBox>().Single(field => field.Name == "WineRunner").Text = Environment.ProcessPath!;
        options.GetVisualDescendants().OfType<TextBox>().Single(field => field.Name == "LaunchArguments").Text = "--name\ntwo words";
        Click(options, "Done"); Dispatcher.UIThread.RunJobs();
        Check(library.State.ManualGames.Single().Launch is null, "Options saved before Apply");
        using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Click(setup, "Save and close");
            Check(!offered.IsCompleted && setup.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Citron:") == true), "Failed save closed setup or hid row error");
            Check(library.State.ManualGames.Single().Launch is null, "Failed launch save changed state");
            CheckActionsAtMinimum(setup, "Apply", "Save and close", "Skip and close");
        }
        Click(setup, "Apply");
        Check(!offered.IsCompleted, "Apply closed the list");
        var saved = new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.ManualGames.Single().Launch!;
        Check(saved.Executable == Path.Combine(root, "citron.exe") && saved.Arguments.SequenceEqual(new[] { "--name", "two words" }), "Apply lost executable/argument boundaries");
        Check(library.State.DontShowManualLaunchPrompt && library.State.SetupManualLaunchOnImport, "Remembered Yes was not saved");
        choice.SelectedItem = choice.Items.Cast<DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.Candidate>().Single(item => item.Path.EndsWith("citron-cmd.exe"));
        Click(setup, "Skip and close"); Until(() => offered.IsCompleted); offered.GetAwaiter().GetResult();
        Check(library.State.ManualGames.Single().Launch!.Executable == saved.Executable, "Skip saved an unconfirmed edit or undid Apply");
        var additionalRoots = Enumerable.Range(1, 44).Select(index => Path.Combine(fixtureRoot, "Game " + index)).ToArray();
        foreach (var additionalRoot in additionalRoots)
        {
            Directory.CreateDirectory(additionalRoot);
            File.WriteAllBytes(Path.Combine(additionalRoot, "game.exe"), [1, 2, 3]);
        }
        library.AddManualGames(additionalRoots);
        library.UpdateState(state =>
        {
            foreach (var game in state.ManualGames.Where(game => game.RootPath != root))
                game.Launch = new(Path.Combine(game.RootPath, "game.exe"), game.RootPath, [], ManualLaunchKind.Wine, Environment.ProcessPath!);
        });
        var list = ManualLaunchSetupWindow.ConfigureAsync(owner, library, library.State.ManualGames.ToArray());
        setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Until(() => Button(setup, "Apply").IsEnabled);
        Check(setup.GetVisualDescendants().OfType<ComboBox>().Count(control => control.Name == "LaunchChoice") == 45, "All games were not in one list");
        Check(setup.Title!.Contains("45 games"), "Header is not for the complete list");
        if (screenshot is not null)
        {
            Dispatcher.UIThread.RunJobs();
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize((int)setup.ClientSize.Width, (int)setup.ClientSize.Height), new Vector(96, 96));
            bitmap.Render(setup); bitmap.Save(screenshot, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        CheckActionsAtMinimum(setup, "Apply", "Save and close", "Skip and close");
        var last = Choice(setup, additionalRoots[^1]);
        var scroll = last.GetVisualAncestors().OfType<ScrollViewer>().First();
        Check(scroll.Extent.Height > scroll.Viewport.Height, "List is not bounded and scrollable");
        last.BringIntoView(); Dispatcher.UIThread.RunJobs();
        var lastPoint = last.TranslatePoint(default, setup)!.Value;
        Check(lastPoint.Y >= 0 && lastPoint.Y + last.Bounds.Height < Button(setup, "Apply").TranslatePoint(default, setup)!.Value.Y, "Last row is inaccessible");
        CheckActionsAtMinimum(setup, "Apply", "Save and close", "Skip and close");
        var firstChoice = Choice(setup, root);
        firstChoice.SelectedItem = firstChoice.Items.Cast<DLSS_Swapper.Data.ManuallyAdded.ManualLaunchManifest.Candidate>().Single(item => item.Path.EndsWith("citron-cmd.exe"));
        File.Delete(Path.Combine(additionalRoots[^1], "game.exe"));
        Click(setup, "Save and close");
        Check(!list.IsCompleted, "Invalid row was silently saved");
        Check(library.State.ManualGames.Single(game => game.RootPath == root).Launch!.Executable.EndsWith("citron-cmd.exe"), "Valid independent row not saved");
        Check(setup.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Game 44:") == true), "Failure did not name its game");
        File.WriteAllBytes(Path.Combine(additionalRoots[^1], "game.exe"), [1, 2, 3]);
        Click(setup, "Save and close"); Until(() => list.IsCompleted); list.GetAwaiter().GetResult();
        Check(new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.ManualGames.Count(game => game.Launch is not null) == 45, "Bulk save lost launch records");
        Check(additionalRoots.All(path => File.ReadAllBytes(Path.Combine(path, "game.exe")).SequenceEqual(new byte[] { 1, 2, 3 })), "Setup changed game executables");
        Check(Directory.GetFiles(root, "*", SearchOption.AllDirectories).All(path => File.ReadAllBytes(path).SequenceEqual(new byte[] { 1, 2, 3 })), "Launch setup changed game files");
        var cancelled = ManualLaunchSetupWindow.ConfigureAsync(owner, library, library.State.ManualGames.ToArray());
        setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Click(setup, "Skip and close"); Until(() => cancelled.IsCompleted);
        Dispatcher.UIThread.RunJobs();
        Console.WriteLine("PASS headless 45-game launch list: bounded layout, ranking, options, Apply, discard, save failure/retry, partial save and scan cancellation");
    }
    private static ComboBox Choice(Window window, string root) => window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "LaunchChoice" && Equals(control.Tag, root));
    private static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
    private static void CheckActionsAtMinimum(Window window, params string[] names)
    {
        window.Width = window.MinWidth; window.Height = window.MinHeight; Dispatcher.UIThread.RunJobs();
        foreach (var name in names)
        {
            var button = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name));
            var point = button.TranslatePoint(default, window)!.Value;
            Check(button.IsVisible && button.Bounds.Width > 0 && button.Bounds.Height > 0
                && point.X >= 0 && point.Y >= 0 && point.X + button.Bounds.Width <= window.ClientSize.Width + 1
                && point.Y + button.Bounds.Height <= window.ClientSize.Height + 1, "Launch action clipped: " + name);
            Check(!button.GetVisualAncestors().OfType<ScrollViewer>().Any(), "Launch action hidden inside scrolling content: " + name);
        }
    }
    private static void Click(Window window, string name) => Button(window, name).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static void Until(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Check(condition(), "Launch setup timed out");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
