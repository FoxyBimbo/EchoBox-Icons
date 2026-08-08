using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using EchoBox.Core.Services;

namespace EchoBox.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        AppLogger.InitializeConsole();

        this.UnhandledException += OnXamlUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        InitializeComponent();
    }

    private void OnXamlUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppLogger.LogError(e.Exception, "WinUI Application.UnhandledException");
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.LogError(ex, "AppDomain.UnhandledException");
        }
        else
        {
            AppLogger.LogError($"Unhandled non-exception object: {e.ExceptionObject}", "AppDomain.UnhandledException");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.LogError(e.Exception, "TaskScheduler.UnobservedTaskException");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
