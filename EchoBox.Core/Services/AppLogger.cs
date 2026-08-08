using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EchoBox.Core.Services;

public static class AppLogger
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    private static bool _isConsoleInitialized = false;
    private static readonly object LogLock = new();

    /// <summary>
    /// Attaches the application output streams to the parent terminal process if launched via command line/terminal.
    /// </summary>
    public static void InitializeConsole()
    {
        if (_isConsoleInitialized) return;
        _isConsoleInitialized = true;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
            }
        }
        catch
        {
            // Ignore if environment does not support AttachConsole
        }
    }

    /// <summary>
    /// Logs an exception with detailed type, message, stack trace, and inner exception info to the terminal and debug output.
    /// </summary>
    public static void LogError(Exception? ex, string context = "")
    {
        if (ex == null) return;
        InitializeConsole();

        lock (LogLock)
        {
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string header = string.IsNullOrWhiteSpace(context)
                ? $"[ERROR] [{timeStamp}]"
                : $"[ERROR] [{timeStamp}] [{context}]";

            string body = $"{header}\n" +
                          $"Type: {ex.GetType().FullName}\n" +
                          $"Message: {ex.Message}\n" +
                          $"StackTrace:\n{ex.StackTrace}";

            if (ex.InnerException != null)
            {
                body += $"\n--- Inner Exception ---\n" +
                        $"Type: {ex.InnerException.GetType().FullName}\n" +
                        $"Message: {ex.InnerException.Message}\n" +
                        $"StackTrace:\n{ex.InnerException.StackTrace}";
            }

            WriteToOutputs(body, ConsoleColor.Red);
        }
    }

    /// <summary>
    /// Logs a text error message to the terminal and debug output.
    /// </summary>
    public static void LogError(string message, string context = "")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        InitializeConsole();

        lock (LogLock)
        {
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string header = string.IsNullOrWhiteSpace(context)
                ? $"[ERROR] [{timeStamp}]"
                : $"[ERROR] [{timeStamp}] [{context}]";

            WriteToOutputs($"{header} {message}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// Logs a warning message to the terminal and debug output.
    /// </summary>
    public static void LogWarning(string message, string context = "")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        InitializeConsole();

        lock (LogLock)
        {
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string header = string.IsNullOrWhiteSpace(context)
                ? $"[WARN]  [{timeStamp}]"
                : $"[WARN]  [{timeStamp}] [{context}]";

            WriteToOutputs($"{header} {message}", ConsoleColor.Yellow);
        }
    }

    /// <summary>
    /// Logs an informational message to the terminal and debug output.
    /// </summary>
    public static void LogInfo(string message, string context = "")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        InitializeConsole();

        lock (LogLock)
        {
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string header = string.IsNullOrWhiteSpace(context)
                ? $"[INFO]  [{timeStamp}]"
                : $"[INFO]  [{timeStamp}] [{context}]";

            WriteToOutputs($"{header} {message}", ConsoleColor.Cyan);
        }
    }

    private static void WriteToOutputs(string formattedMessage, ConsoleColor color)
    {
        // Debug window (Visual Studio / VS Code Debug Console)
        Debug.WriteLine(formattedMessage);
        Trace.WriteLine(formattedMessage);

        // Standard Terminal Output
        try
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Error.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
            foreach (var line in formattedMessage.Split('\n'))
            {
                Console.Error.WriteLine($"│ {line.TrimEnd('\r')}");
            }
            Console.Error.WriteLine("└──────────────────────────────────────────────────────────────────────────────┘");
            Console.ForegroundColor = prevColor;
        }
        catch
        {
            // Fallback if Console standard handle is unavailable
        }
    }
}
