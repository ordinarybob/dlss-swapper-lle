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
    internal static void Run(Window owner, string fixtureRoot)
    {
        var root = Path.Combine(fixtureRoot, "Citron"); Directory.CreateDirectory(root);
        foreach (var name in new[] { "citron.exe", "citron-cmd.exe" })
            File.WriteAllBytes(Path.Combine(root, name), [1,2,3]);
        Directory.CreateDirectory(Path.Combine(root, "Artbook"));
        File.WriteAllBytes(Path.Combine(root, "Artbook", "citron.exe"), [1,2,3]);
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
        Until(() => Field<TextBox>(setup, "Executable").Text == Path.Combine(root, "citron.exe"));
        CheckActionsAtMinimum(setup, "Browse for executable…", "Browse for Wine…", "Save", "Skip game", "Finish later");
        var choices = Field<ComboBox>(setup, "Executable suggestions").Items.Cast<object>().Select(item => item.ToString()!).ToArray();
        Check(choices.All(text => !text.Contains("Artbook", StringComparison.OrdinalIgnoreCase)), "Excluded executable suggested");
        var options = setup.GetVisualDescendants().OfType<Expander>().Single();
        Check(!options.IsExpanded, "Advanced launch fields were shown by default");
        options.IsExpanded = true; Dispatcher.UIThread.RunJobs();
        Field<TextBox>(setup, "Wine executable").Text = Environment.ProcessPath!;
        Field<TextBox>(setup, "Arguments (one argument per line; do not add shell quoting)").Text = "--name\ntwo words";
        using (var held = new FileStream(Path.Combine(library.StateDirectory, ".state.write.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Click(setup, "Save");
            Check(!offered.IsCompleted && setup.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Could not save:") == true), "Failed save closed setup or hid error");
            Check(library.State.ManualGames.Single().Launch is null, "Failed launch save changed state");
            CheckActionsAtMinimum(setup, "Browse for executable…", "Browse for Wine…", "Save", "Skip game", "Finish later");
        }
        Click(setup, "Save"); Until(() => offered.IsCompleted); offered.GetAwaiter().GetResult();
        var saved = new PersistentLibrary(new LibraryStateStore(library.StateDirectory)).State.ManualGames.Single().Launch!;
        Check(saved.Executable == Path.Combine(root, "citron.exe") && saved.Arguments.SequenceEqual(new[] { "--name", "two words" }), "Launch setup did not preserve executable/argument boundaries");
        Check(library.State.DontShowManualLaunchPrompt && library.State.SetupManualLaunchOnImport, "Remembered Yes was not saved");
        var editing = ManualLaunchSetupWindow.OfferAsync(owner, library, library.State.ManualGames.ToArray());
        setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Check(Field<TextBox>(setup, "Executable").Text == saved.Executable, "Editing lost saved executable");
        Field<TextBox>(setup, "Executable").Text = Path.Combine(root, "citron-cmd.exe");
        Click(setup, "Finish later"); Until(() => editing.IsCompleted); editing.GetAwaiter().GetResult();
        Check(library.State.ManualGames.Single().Launch!.Executable == saved.Executable, "Finish later saved an unconfirmed edit");
        var additionalRoots = new[] { "Second", "Third" }.Select(name => Path.Combine(fixtureRoot, name)).ToArray();
        foreach (var additionalRoot in additionalRoots)
        {
            Directory.CreateDirectory(additionalRoot);
            File.WriteAllBytes(Path.Combine(additionalRoot, "game.exe"), [1,2,3]);
        }
        library.AddManualGames(additionalRoots);
        var sequence = new[] { library.State.ManualGames.Single(game => game.RootPath == root) }
            .Concat(additionalRoots.Select(path => library.State.ManualGames.Single(game => game.RootPath == path))).ToArray();
        var wizard = ManualLaunchSetupWindow.ConfigureAsync(owner, library, sequence);
        setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Check(setup.Title!.Contains("(1/3)"), "Wizard omitted first step position");
        Field<TextBox>(setup, "Executable").Text = Path.Combine(root, "citron-cmd.exe");
        Click(setup, "Skip game");
        Until(() => owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Any(window => window.Title!.Contains("(2/3)")));
        setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Check(library.State.ManualGames.Single(game => game.RootPath == root).Launch!.Executable == saved.Executable, "Skip game persisted draft edits");
        Field<TextBox>(setup, "Executable").Text = Path.Combine(additionalRoots[0], "game.exe");
        setup.GetVisualDescendants().OfType<Expander>().Single().IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        Field<TextBox>(setup, "Wine executable").Text = Environment.ProcessPath!;
        Click(setup, "Save and next");
        Until(() => owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Any(window => window.Title!.Contains("(3/3)")));
        setup = owner.OwnedWindows.OfType<ManualLaunchSetupWindow>().Single();
        Click(setup, "Finish later"); Until(() => wizard.IsCompleted); wizard.GetAwaiter().GetResult();
        var reloaded = new PersistentLibrary(new LibraryStateStore(library.StateDirectory));
        Check(reloaded.State.ManualGames.Single(game => game.RootPath == additionalRoots[0]).Launch?.Executable == Path.Combine(additionalRoots[0], "game.exe"), "Save and next did not persist the completed step");
        Check(reloaded.State.ManualGames.Single(game => game.RootPath == additionalRoots[1]).Launch is null, "Finish later configured an unfinished step");
        Check(additionalRoots.All(path => File.ReadAllBytes(Path.Combine(path, "game.exe")).SequenceEqual(new byte[] {1,2,3})), "Wizard changed game executables");
        Check(Directory.GetFiles(root, "*", SearchOption.AllDirectories).All(path => File.ReadAllBytes(path).SequenceEqual(new byte[] {1,2,3})), "Launch setup changed game files");
        Console.WriteLine("PASS headless manual launch offer/ranking/exclusions/save failure/retry/edit cancel (not native Linux acceptance)");
    }
    private static T Field<T>(Window window, string label) where T : Control
    {
        var text = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == label);
        var panel = (StackPanel)text.Parent!;
        return (T)panel.Children[panel.Children.IndexOf(text) + 1];
    }
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
    private static void Click(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Until(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Check(condition(), "Launch setup timed out");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
