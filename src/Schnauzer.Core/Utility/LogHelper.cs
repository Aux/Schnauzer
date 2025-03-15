using Discord;
using Microsoft.Extensions.Logging;

namespace Schnauzer;

public static class LogHelper
{
    public static Task OnLogAsync(ILogger logger, LogMessage msg)
    {
        switch (msg.Severity)
        {
            case LogSeverity.Verbose:
                logger.LogInformation($"{msg}");
                break;

            case LogSeverity.Info:
                logger.LogInformation($"{msg}");
                break;

            case LogSeverity.Warning:
                logger.LogWarning($"{msg}");
                break;

            case LogSeverity.Error:
                logger.LogError($"{msg}");
                break;

            case LogSeverity.Critical:
                logger.LogCritical($"{msg}");
                break;
        }
        return Task.CompletedTask;
    }
}