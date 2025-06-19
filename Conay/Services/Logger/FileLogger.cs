using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Conay.Services.Logger;

public class FileLogger(string categoryName, string filePath) : ILogger
{
    IDisposable? ILogger.BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {categoryName}: {formatter(state, exception)}";

        if (exception != null)
        {
            message += Environment.NewLine + exception;
        }

        File.AppendAllText(filePath, message + Environment.NewLine);
    }
}