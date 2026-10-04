using System;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DLSS_Swapper;

public sealed partial class TranslationToolboxWindow : Window
{
    public TranslationToolboxWindowModel ViewModel { get; private set; }

    public TranslationToolboxWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon("Assets\\icon.ico");

        ViewModel = new TranslationToolboxWindowModel(this);

        ResourceHelper.TranslatorModeEnabled = true;
        Closed += TranslationToolboxWindow_Closed;
    }

    async void TranslationToolboxWindow_Closed(object sender, WindowEventArgs args)
    {
        if (ViewModel.HasUnsavedChanges() == true)
        {
            args.Handled = true;
            var dialog = new EasyContentDialog(Content.XamlRoot)
            {
                Title = ResourceHelper.GetString("TranslationToolboxPage_UnsavedChangesTitle"),
                DefaultButton = ContentDialogButton.Close,
                Content = ResourceHelper.GetString("TranslationToolboxPage_UnsavedChangesMessage"),
                CloseButtonText = ResourceHelper.GetString("General_Cancel"),
                PrimaryButtonText = ResourceHelper.GetString("TranslationToolboxPage_UnsavedChangesButton"),
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
            {
                return;
            }
        }

        ResourceHelper.TranslatorModeEnabled = false;

        Closed -= TranslationToolboxWindow_Closed;
        Close();
    }

    void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var shiftStatus = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.LeftShift) |
                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.RightShift) |
                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            if (sender is TextBox textBox)
            {
                if (shiftStatus.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    int selectionStart = textBox.SelectionStart;
                    int selectionLength = textBox.SelectionLength;

                    textBox.Text = textBox.Text.Remove(selectionStart, selectionLength);
                    textBox.Text = textBox.Text.Insert(selectionStart, Environment.NewLine);

                    textBox.SelectionStart = selectionStart + Environment.NewLine.Length;
                    e.Handled = true;
                }
                else
                {
                    var currentColumn = MainDataGrid.CurrentColumn;
                    var currentRowIndex = MainDataGrid.SelectedIndex;
                    var newRowIndex = currentRowIndex + 1;
                    if (newRowIndex < ViewModel.TranslationRows.Count)
                    {
                        MainDataGrid.SelectedIndex = newRowIndex;
                        MainDataGrid.CurrentColumn = currentColumn;

                        MainDataGrid.ScrollIntoView(ViewModel.TranslationRows[newRowIndex], currentColumn);

                        MainDataGrid.Focus(FocusState.Programmatic);
                        MainDataGrid.BeginEdit();
                    }
                    else
                    {
                    }

                    e.Handled = true;
                }
            }
        }
    }

    void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.RecalculateTranslationProgress();
    }

}
