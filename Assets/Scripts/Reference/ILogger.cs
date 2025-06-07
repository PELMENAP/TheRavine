public interface ILogger {
    void LogWarning(string message);
    void LogError(string message);
    void LogInfo(string message);
}