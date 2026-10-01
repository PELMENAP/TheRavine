using System;

public static class RavineLog
{
    private static RavineLogger _logger;

    public static bool IsInitialized => _logger != null;

    public static void Initialize(RavineLogger logger)
    {
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        _logger = logger;
    }

    public static void Shutdown()
    {
        _logger = null;
    }

    public static void Error(string message)
    {
        _logger?.LogError(message);
    }

    public static void Warning(string message)
    {
        _logger?.LogWarning(message);
    }

    public static void Info(string message)
    {
        _logger?.LogInfo(message);
    }

    public static void Critical(string message)
    {
        _logger?.LogCritical(message);
    }
}