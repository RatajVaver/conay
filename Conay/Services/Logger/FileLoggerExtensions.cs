using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conay.Services.Logger;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
    {
        builder.Services.AddSingleton<ILoggerProvider>(new FileLoggerProvider(filePath));
        return builder;
    }
}
