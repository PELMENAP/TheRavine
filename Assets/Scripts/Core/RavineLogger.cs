using Cysharp.Text; // Для ZString
using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using System.Text;
using UnityEngine;

public class RavineLogger
{
    private readonly string logFilePath;
    private readonly Action<string> onMessageDisplayTerminal;
    private readonly HashSet<string> loggedErrors = new();
    private byte criticalErrorNumber;
    private readonly byte maxCriticalNumber = 10;

    private const int FlushChars = 8192;
    private const long MaxFileChars = 16L * 1024 * 1024;

    private readonly StringBuilder pending = new(FlushChars * 2);
    private readonly object fileLock = new();
    private long fileChars;
    private long lastFlushTicks;
    private bool fileLimitReached;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private readonly bool isDebugBuild = true;
#else
    private bool isDebugBuild = false;
#endif
    
    private const string ErrorPrefix = "[ERROR] ";
    private const string WarningPrefix = "[WARNING] ";
    private const string InfoPrefix = "[INFO] ";
    private const string CriticalPrefix = "[CRITICAL] ";
    
    private const string StyledErrorPrefix = "<color=red>[ERROR]</color> ";
    private const string StyledWarningPrefix = "<color=orange>[WARNING]</color> ";
    private const string StyledInfoPrefix = "<color=green>[INFO]</color> ";
    private const string StyledCriticalPrefix = "<color=purple><b>[CRITICAL]</b></color> ";

    public RavineLogger(Action<string> onMessageDisplayTerminal, string logFileName = "game_log.txt")
    {
        criticalErrorNumber = 0;
        this.onMessageDisplayTerminal = onMessageDisplayTerminal;
        logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        
        File.WriteAllText(logFilePath, ZString.Concat("Game Log Started: ", DateTime.Now, "\n"));
        lastFlushTicks = Stopwatch.GetTimestamp();
        Application.quitting += Flush;
    }

    public void LogError(string message)
    {
        if (loggedErrors.Contains(message))
        {
            criticalErrorNumber++;
        }

        string logMessage = ZString.Concat(ErrorPrefix, message);
        string styledMessage = ZString.Concat(StyledErrorPrefix, message);
        
        if (isDebugBuild)
        {
            Debug.LogError(logMessage);
        }
        
        WriteToFile(logMessage, true);
        DisplayInGameConsole(styledMessage);

        loggedErrors.Add(message);

        if (criticalErrorNumber > maxCriticalNumber)
            StopApplication();
    }

    public void LogWarning(string message)
    {
        string logMessage = ZString.Concat(WarningPrefix, message);
        string styledMessage = ZString.Concat(StyledWarningPrefix, message);
        
        if (isDebugBuild)
        {
            Debug.LogWarning(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(styledMessage);
    }

    public void LogInfo(string message)
    {
        string logMessage = ZString.Concat(InfoPrefix, message);
        string styledMessage = ZString.Concat(StyledInfoPrefix, message);
        
        if (isDebugBuild)
        {
            Debug.Log(logMessage);
        }
        
        WriteToFile(logMessage);
        DisplayInGameConsole(styledMessage);
    }

    public void LogCritical(string message)
    {
        criticalErrorNumber++;
        
        string logMessage = ZString.Concat(CriticalPrefix, message);
        string styledMessage = ZString.Concat(StyledCriticalPrefix, message);
        
        if (isDebugBuild)
        {
            Debug.LogError(logMessage);
        }
        
        WriteToFile(logMessage, true);
        DisplayInGameConsole(styledMessage);

        if (criticalErrorNumber > maxCriticalNumber)
            StopApplication();
    }

    private void WriteToFile(string message, bool flushNow = false)
    {
        lock (fileLock)
        {
            if (fileLimitReached) return;

            using var line = ZString.CreateStringBuilder();
            line.Append(DateTime.Now);
            line.Append(": ");
            line.Append(message);
            line.Append('\n');

            fileChars += line.Length;
            if (fileChars > MaxFileChars)
            {
                fileLimitReached = true;
                pending.Append(ZString.Concat(DateTime.Now, ": ", CriticalPrefix, "Log size limit reached, file logging stopped\n"));
                FlushLocked();
                return;
            }

            pending.Append(line.AsSpan());

            if (flushNow || pending.Length >= FlushChars || Stopwatch.GetTimestamp() - lastFlushTicks >= Stopwatch.Frequency)
                FlushLocked();
        }
    }

    public void Flush()
    {
        lock (fileLock) FlushLocked();
    }

    private void FlushLocked()
    {
        lastFlushTicks = Stopwatch.GetTimestamp();
        if (pending.Length == 0) return;

        try
        {
            File.AppendAllText(logFilePath, pending.ToString());
        }
        catch (Exception ex)
        {
            string errorMsg = ZString.Concat("Failed to write to log file: ", ex.Message);
            
            if (isDebugBuild)
            {
                Debug.LogError(ZString.Concat(ErrorPrefix, errorMsg));
            }
            
            onMessageDisplayTerminal?.Invoke(ZString.Concat(StyledErrorPrefix, errorMsg));
        }
        pending.Clear();
    }

    private void DisplayInGameConsole(string styledMessage)
    {
        if (onMessageDisplayTerminal == null)
        {
            string errorMsg = "onMessageDisplayTerminal action not exist";
            WriteToFile(ZString.Concat(ErrorPrefix, errorMsg));
        }
        else
        {
            onMessageDisplayTerminal.Invoke(styledMessage);
        }
    }

    private void StopApplication()
    {
        string criticalMsg = "Too many critical errors occurred. Application will now close.";
        
        Debug.LogError(criticalMsg);
        WriteToFile(ZString.Concat(CriticalPrefix, criticalMsg), true);
        
        onMessageDisplayTerminal?.Invoke(ZString.Concat(StyledCriticalPrefix, criticalMsg));
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
