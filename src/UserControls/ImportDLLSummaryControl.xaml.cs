using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.UserControls;

public sealed partial class ImportDLLSummaryControl : UserControl
{
    ImportDLLSummaryControlModel ViewModel { get; }

    public ImportDLLSummaryControl(IReadOnlyList<DLLImportResult> dllImportResults)
    {
        ArgumentNullException.ThrowIfNull(dllImportResults);
        this.InitializeComponent();
        ViewModel = new ImportDLLSummaryControlModel(dllImportResults);
        DataContext = ViewModel;
    }
}
