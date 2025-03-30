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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool isDebugBuild = true;
#else
    private bool isDebugBuild = false;
#endif

    public Logger(Action<string> onMessageDisplayTerminal, string logFileName = "game_log.txt")
    {
        criticalErrorNumber = 0;
        this.onMessageDisplayTerminal = onMessageDisplayTerminal;
        logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        File.WriteAllText(logFilePath, $"Game Log Started: {DateTime.Now}\n");
    }

    public void LogError(string message)
    {
        if (loggedErrors.Contains(message))
        {
            criticalErrorNumber++;
            
            if(criticalErrorNumber > maxCriticalNumber)
                StopApplication();
                
            return;
        }

        string logMessage = $"[ERROR] {message}";
        
        if (isDebugBuild)
        {
            Debug.LogError(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);

        loggedErrors.Add(message);

        if(criticalErrorNumber > maxCriticalNumber)
            StopApplication();
    }

    public void LogWarning(string message)
    {
        string logMessage = $"[WARNING] {message}";
        
        if (isDebugBuild)
        {
            Debug.LogWarning(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);
    }

    public void LogInfo(string message)
    {
        string logMessage = $"[INFO] {message}";
        
        if (isDebugBuild)
        {
            Debug.Log(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);
    }
    private void WriteToFile(string message)
    {
        try
        {
            File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}\n");
        }
        catch (Exception ex)
        {
            if (isDebugBuild)
            {
                Debug.LogError($"Failed to write to log file: {ex.Message}");
            }
            
            if (onMessageDisplayTerminal != null)
            {
                onMessageDisplayTerminal.Invoke($"[ERROR] Failed to write to log file: {ex.Message}");
            }
        }
    }

    private void DisplayInGameConsole(string message)
    {
        if(onMessageDisplayTerminal == null)
        {
            WriteToFile("[ERROR] onMessageDisplayTerminal action not exist");
        }
        else
        {
            onMessageDisplayTerminal.Invoke(message);
        }
    }

    private void StopApplication()
    {
        WriteToFile("[CRITICAL] Too many critical errors occurred. Application will now close.");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}