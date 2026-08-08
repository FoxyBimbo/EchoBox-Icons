using System;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using EchoBox.Engine.Services;

namespace EchoBox.App.Views;

public partial class ApplyProgressDialog : ContentDialog
{
    private readonly CancellationTokenSource _cts;

    public ApplyProgressDialog(CancellationTokenSource cts)
    {
        InitializeComponent();
        _cts = cts;
        CloseButtonClick += ContentDialog_CloseButtonClick;
    }

    public void ReportProgress(ApplyProgressReport report)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusTextBlock.Text = report.StatusMessage;
            ScannedCountTextBlock.Text = $"Scanned items: {report.GetScannedCount():N0}";
            UpdatedCountTextBlock.Text = $"Updated icons: {report.GetUpdatedCount():N0}";
            CurrentPathTextBlock.Text = string.IsNullOrEmpty(report.CurrentItemPath) ? "Path: -" : $"Path: {report.CurrentItemPath}";

            if (report.IsCompleted)
            {
                IndeterminateProgressBar.IsIndeterminate = false;
                IndeterminateProgressBar.Value = 100;
                CloseButtonText = "Close";
            }
        });
    }

    private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _cts.Cancel();
    }
}
