using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HeadlessSimulation
{
    private const string ScenePath = "Assets/Scenes/TestScene/FreeScene.unity";
    private const string TempFolder = "Assets/_HeadlessTemp";
    private const string ActiveKey = "HeadlessSimulation.Active";
    private const string SceneKey = "HeadlessSimulation.Scene";
    private const string RulesKey = "HeadlessSimulation.Rules";
    private const string TempRulesKey = "HeadlessSimulation.TempRules";
    private const string SecondsKey = "HeadlessSimulation.Seconds";
    private const string TicksKey = "HeadlessSimulation.Ticks";
    private const string LastKey = "HeadlessSimulation.Last";
    private const string IndexKey = "HeadlessSimulation.Index";
    private const string OutKey = "HeadlessSimulation.Out";
    private const string FpsKey = "HeadlessSimulation.Fps";
    private const string WallKey = "HeadlessSimulation.Wall";
    private const string FailureKey = "HeadlessSimulation.Failure";
    private const string StartUtcKey = "HeadlessSimulation.StartUtc";
    private const string StartFrameKey = "HeadlessSimulation.StartFrame";
    private const string StartWallKey = "HeadlessSimulation.StartWall";
    private const string StoppingKey = "HeadlessSimulation.Stopping";
    private const string LogPrefix = "[HeadlessSimulation] ";
    private const string LogFile = "simulation_log_";

    static HeadlessSimulation()
    {
        if (SessionState.GetBool(ActiveKey, false)) Subscribe();
    }

    public static void Run()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            string scene = ReadString(args, "-simScene", ScenePath);
            string rules = ReadString(args, "-simRules", string.Empty);
            float seconds = ReadFloat(args, "-simSeconds", 0f);
            int ticks = ReadInt(args, "-simTicks", 0);
            int replicates = ReadInt(args, "-simReplicates", 1);
            int first = ReadInt(args, "-simFirstReplicate", 0);
            string output = ReadString(args, "-simOut", string.Empty);
            int fps = ReadInt(args, "-simFps", 30);
            float wall = ReadFloat(args, "-simWallSeconds", 0f);

            if (seconds <= 0f && ticks <= 0) throw new ArgumentException("set -simSeconds or -simTicks");
            if (replicates < 1) throw new ArgumentException("-simReplicates must be at least 1");
            if (first < 0) throw new ArgumentException("-simFirstReplicate must not be negative");
            if (fps < 1) throw new ArgumentException("-simFps must be at least 1");
            if (string.IsNullOrEmpty(output)) throw new ArgumentException("set -simOut");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new ArgumentException($"scene not found: {scene}");

            Directory.CreateDirectory(output);

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(SceneKey, scene);
            SessionState.SetString(RulesKey, rules);
            SessionState.SetString(TempRulesKey, string.Empty);
            SessionState.SetFloat(SecondsKey, seconds);
            SessionState.SetInt(TicksKey, ticks);
            SessionState.SetInt(IndexKey, first);
            SessionState.SetInt(LastKey, first + replicates - 1);
            SessionState.SetString(OutKey, Path.GetFullPath(output));
            SessionState.SetInt(FpsKey, fps);
            SessionState.SetFloat(WallKey, wall);
            SessionState.SetString(FailureKey, string.Empty);

            Subscribe();
            StartReplicate();
        }
        catch (Exception e)
        {
            Debug.LogError(LogPrefix + e);
            Finish(1);
        }
    }

    private static void Subscribe()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    private static void StartReplicate()
    {
        SessionState.SetBool(StoppingKey, false);
        SessionState.SetString(FailureKey, string.Empty);

        EditorSceneManager.OpenScene(SessionState.GetString(SceneKey, ScenePath), OpenSceneMode.Single);
        var manager = UnityEngine.Object.FindFirstObjectByType<EntityManager>(FindObjectsInactive.Include);
        if (manager == null) throw new InvalidOperationException("the scene has no EntityManager");

        string rulesPath = SessionState.GetString(RulesKey, string.Empty);
        SimulationRules rules = string.IsNullOrEmpty(rulesPath) ? null : AssetDatabase.LoadAssetAtPath<SimulationRules>(rulesPath);
        if (!string.IsNullOrEmpty(rulesPath) && rules == null) throw new ArgumentException($"SimulationRules not found: {rulesPath}");

        var managerObject = new SerializedObject(manager);
        var rulesProperty = managerObject.FindProperty("rules");
        if (rules == null) rules = rulesProperty.objectReferenceValue as SimulationRules;
        if (rules == null) throw new InvalidOperationException("the EntityManager has no SimulationRules assigned");

        rules = EnsureLogEnabled(rules);
        rulesProperty.objectReferenceValue = rules;
        managerObject.ApplyModifiedPropertiesWithoutUndo();

        SessionState.SetString(StartUtcKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        Debug.Log($"{LogPrefix}replicate {SessionState.GetInt(IndexKey, 0)}: scene {SessionState.GetString(SceneKey, ScenePath)}, rules {AssetDatabase.GetAssetPath(rules)}");
        EditorApplication.EnterPlaymode();
    }

    private static SimulationRules EnsureLogEnabled(SimulationRules rules)
    {
        var serialized = new SerializedObject(rules);
        var flag = serialized.FindProperty("simulationLogEnabled");
        if (flag == null) throw new InvalidOperationException("SimulationRules has no simulationLogEnabled");
        if (flag.boolValue) return rules;

        if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "_HeadlessTemp");
        string path = $"{TempFolder}/{rules.name}.headless.asset";
        AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(rules), path)) throw new InvalidOperationException($"cannot copy {rules.name} to {path}");
        AssetDatabase.ImportAsset(path);
        var copy = AssetDatabase.LoadAssetAtPath<SimulationRules>(path);
        var copyObject = new SerializedObject(copy);
        copyObject.FindProperty("simulationLogEnabled").boolValue = true;
        copyObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(copy);
        AssetDatabase.SaveAssets();
        SessionState.SetString(TempRulesKey, path);
        return copy;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        try
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SimulationClock.Reset();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    Application.targetFrameRate = -1;
                    QualitySettings.vSyncCount = 0;
                    Time.captureFramerate = SessionState.GetInt(FpsKey, 30);
                    SessionState.SetInt(StartFrameKey, Time.frameCount);
                    SessionState.SetString(StartWallKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    CompleteReplicate();
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogPrefix + e);
            Finish(1);
        }
    }

    private static void Poll()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (SessionState.GetBool(StoppingKey, false)) return;

        string failure = SessionState.GetString(FailureKey, string.Empty);
        bool stop = failure.Length > 0;

        if (!stop)
        {
            float seconds = SessionState.GetFloat(SecondsKey, 0f);
            int ticks = SessionState.GetInt(TicksKey, 0);
            if (seconds > 0f && SimulationClock.TimeD >= seconds) stop = true;
            if (ticks > 0 && Time.frameCount - SessionState.GetInt(StartFrameKey, 0) >= ticks) stop = true;
        }

        if (!stop)
        {
            float wall = SessionState.GetFloat(WallKey, 0f);
            if (wall > 0f && double.TryParse(SessionState.GetString(StartWallKey, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double started)
                && EditorApplication.timeSinceStartup - started > wall)
            {
                SessionState.SetString(FailureKey, $"wall-clock limit of {wall.ToString(CultureInfo.InvariantCulture)} s reached at simulation time {SimulationClock.TimeD.ToString("F1", CultureInfo.InvariantCulture)} s");
                stop = true;
            }
        }

        if (!stop) return;
        SessionState.SetBool(StoppingKey, true);
        EditorApplication.ExitPlaymode();
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception) return;
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (SessionState.GetString(FailureKey, string.Empty).Length > 0) return;
        SessionState.SetString(FailureKey, condition);
    }

    private static void CompleteReplicate()
    {
        string failure = SessionState.GetString(FailureKey, string.Empty);
        int index = SessionState.GetInt(IndexKey, 0);
        if (failure.Length == 0) CollectOutputs(index, out failure);

        if (failure.Length > 0)
        {
            Debug.LogError($"{LogPrefix}replicate {index} failed: {failure}");
            Finish(1);
            return;
        }

        Debug.Log($"{LogPrefix}replicate {index} done");
        if (index >= SessionState.GetInt(LastKey, index))
        {
            Finish(0);
            return;
        }

        SessionState.SetInt(IndexKey, index + 1);
        StartReplicate();
    }

    private static bool CollectOutputs(int index, out string failure)
    {
        failure = string.Empty;
        string output = SessionState.GetString(OutKey, string.Empty);
        string source = NeuralModelStorage.BaseDirectory;
        long.TryParse(SessionState.GetString(StartUtcKey, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long startTicks);
        DateTime since = new DateTime(startTicks, DateTimeKind.Utc).AddSeconds(-1);

        bool hasSimulationLog = false;
        if (Directory.Exists(source))
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string ext = Path.GetExtension(file);
                if (ext != ".csv" && ext != ".json") continue;
                if (File.GetLastWriteTimeUtc(file) < since) continue;
                string name = Path.GetFileName(file);
                string target = Path.Combine(output, $"r{index:D3}_{name}");
                if (File.Exists(target)) File.Delete(target);
                File.Move(file, target);
                if (name.StartsWith(LogFile, StringComparison.Ordinal)) hasSimulationLog = true;
            }
        }

        if (!hasSimulationLog)
        {
            failure = $"no {LogFile}*.csv was written to {source}: the simulation did not start or simulationLogEnabled is off";
            return false;
        }
        return true;
    }

    private static void Finish(int code)
    {
        string temp = SessionState.GetString(TempRulesKey, string.Empty);
        if (!string.IsNullOrEmpty(temp)) AssetDatabase.DeleteAsset(temp);
        if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);

        SessionState.SetBool(ActiveKey, false);
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.update -= Poll;
        Application.logMessageReceived -= OnLog;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.Exit(code);
    }

    private static string ReadString(string[] args, string name, string fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }

    private static int ReadInt(string[] args, string name, int fallback)
    {
        string text = ReadString(args, name, null);
        if (text == null) return fallback;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) throw new ArgumentException($"{name} is not an integer: {text}");
        return value;
    }

    private static float ReadFloat(string[] args, string name, float fallback)
    {
        string text = ReadString(args, name, null);
        if (text == null) return fallback;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) throw new ArgumentException($"{name} is not a number: {text}");
        return value;
    }
}
