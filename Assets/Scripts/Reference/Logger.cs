using Cysharp.Text; // Для ZString
using System;
using System.Collections.Generic;
using System.IO;
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
    private const string ErrorPrefix = "[ERROR] ";
    private const string WarningPrefix = "[WARNING] ";
    private const string InfoPrefix = "[INFO] ";
    private const string CriticalPrefix = "[CRITICAL] ";

    public Logger(Action<string> onMessageDisplayTerminal, string logFileName = "game_log.txt")
    {
        criticalErrorNumber = 0;
        this.onMessageDisplayTerminal = onMessageDisplayTerminal;
        logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        
        File.WriteAllText(logFilePath, ZString.Concat("Game Log Started: ", DateTime.Now, "\n"));
    }

    public void LogError(string message)
    {
        if (loggedErrors.Contains(message))
        {
            criticalErrorNumber++;
            
            if (criticalErrorNumber > maxCriticalNumber)
                StopApplication();
                
            return;
        }

        string logMessage = ZString.Concat(ErrorPrefix, message);
        
        if (isDebugBuild)
        {
            Debug.LogError(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);

        loggedErrors.Add(message);

        if (criticalErrorNumber > maxCriticalNumber)
            StopApplication();
    }

    public void LogWarning(string message)
    {
        string logMessage = ZString.Concat(WarningPrefix, message);
        
        if (isDebugBuild)
        {
            Debug.LogWarning(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(logMessage);
    }

    public void LogInfo(string message)
    {
        string logMessage = ZString.Concat(InfoPrefix, message);
        
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
            using (var sb = ZString.CreateUtf8StringBuilder())
            {
                sb.Append(DateTime.Now);
                sb.Append(": ");
                sb.Append(message);
                sb.Append("\n");
                
                File.AppendAllText(logFilePath, sb.ToString());
            }
        }
        catch (Exception ex)
        {
            if (isDebugBuild)
            {
                Debug.LogError(ZString.Concat("Failed to write to log file: ", ex.Message));
            }
            
            onMessageDisplayTerminal?.Invoke(ZString.Concat(ErrorPrefix, "Failed to write to log file: ", ex.Message));
        }
    }

    private void DisplayInGameConsole(string message)
    {
        if (onMessageDisplayTerminal == null)
        {
            WriteToFile(ZString.Concat(ErrorPrefix, "onMessageDisplayTerminal action not exist"));
        }
        else
        {
            onMessageDisplayTerminal.Invoke(message);
        }
    }

    private void StopApplication()
    {
        WriteToFile(ZString.Concat(CriticalPrefix, "Too many critical errors occurred. Application will now close."));
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}