using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Logger : ILogger
{
    private string logFilePath;
    private Action<string> onMessageDisplayTerminal;
    private HashSet<string> loggedErrors = new HashSet<string>();
    private byte criticalErrorNumber, maxCriticalNumber = 5;

    public Logger(Action<string> onMessageDisplayTerminal, string logFileName = "game_log.txt")
    {
        criticalErrorNumber = 0;
        this.onMessageDisplayTerminal = onMessageDisplayTerminal;
        logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        File.WriteAllText(logFilePath, "Game Log Started\n");
    }

    public void LogError(string message)
    {
        if (loggedErrors.Contains(message))
        {
            criticalErrorNumber++;
            return;
        }

        string logMessage = $"[ERROR] {message}";
        Debug.LogError(logMessage);
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);

        loggedErrors.Add(message);

        if(criticalErrorNumber > maxCriticalNumber)
            StopApplication();
    }

    public void LogWarning(string message)
    {
        string logMessage = $"[WARNING] {message}";
        Debug.LogWarning(logMessage);
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);
    }

    public void LogInfo(string message)
    {
        string logMessage = $"[INFO] {message}";
        Debug.Log(logMessage);
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);
    }

    private void WriteToFile(string message)
    {
        try
        {
            File.AppendAllText(logFilePath, $"{System.DateTime.Now}: {message}\n");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write to log file: {ex.Message}");
        }
    }

    private void DisplayInGameConsole(string message)
    {
        if(onMessageDisplayTerminal == null)
            WriteToFile("[ERROR] onMessageDisplayTerminal action not exist");
        else
            onMessageDisplayTerminal.Invoke(message);
    }

    private void StopApplication()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}