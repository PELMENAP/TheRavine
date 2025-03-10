using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LLMUnity
{
    [DefaultExecutionOrder(-2)]
    public class LLM : LLMClient
    {
        /// <summary> number of threads to use (-1 = all) </summary>
        [Server] public int numThreads = -1;
        /// <summary> number of model layers to offload to the GPU (0 = GPU not used).
        /// Use a large number i.e. >30 to utilise the GPU as much as possible.
        /// If the user's GPU is not supported, the LLM will fall back to the CPU </summary>
        [Server] public int numGPULayers = 0;
        /// <summary> number of prompts that can happen in parallel (-1 = number of LLM/LLMClient objects) </summary>
        [ServerAdvanced] public int parallelPrompts = -1;
        /// <summary> select to log the output of the LLM in the Unity Editor. </summary>
        [ServerAdvanced] public bool debug = false;
        /// <summary> allows to start the server asynchronously.
        /// This is useful to not block Unity while the server is initialised.
        /// For example it can be used as follows:
        /// \code
        /// void Start(){
        ///     StartCoroutine(Loading());
        ///     ...
        /// }
        ///
        /// IEnumerator<string> Loading()
        /// {
        ///     // show loading screen
        ///     while (!llm.serverListening)
        ///     {
        ///         yield return null;
        ///     }
        ///     Debug.Log("Server is ready");
        /// }
        /// \endcode
        /// </summary>
        [ServerAdvanced] public bool asynchronousStartup = false;
        /// <summary> select to allow remote access to the server. </summary>
        [ServerAdvanced] public bool remote = false;
        /// <summary> Select to kill existing servers created by the package at startup.
        /// Useful in case of game crashes where the servers didn't have the chance to terminate</summary>
        [ServerAdvanced] public bool killExistingServersOnStart = true;

        /// <summary> the path of the model being used (relative to the Assets/StreamingAssets folder).
        /// Models with .gguf format are allowed.</summary>
        [Model] public string model = "";
        /// <summary> the path of the LORA model being used (relative to the Assets/StreamingAssets folder).
        /// Models with .bin format are allowed.</summary>
        [ModelAddonAdvanced] public string lora = "";
        /// <summary> Size of the prompt context (0 = context size of the model).
        /// This is the number of tokens the model can take as input when generating responses. </summary>
        [ModelAdvanced] public int contextSize = 512;
        /// <summary> Batch size for prompt processing. </summary>
        [ModelAdvanced] public int batchSize = 512;
        /// <summary> Boolean set to true if the server has started and is ready to receive requests, false otherwise. </summary>
        public bool serverListening { get; private set; } = false;
        /// <summary> Boolean set to true if the server as well as the client functionality has fully started, false otherwise. </summary>
        public bool serverStarted { get; private set; } = false;

        /// \cond HIDE
        [HideInInspector] public readonly (string, string)[] modelOptions = new(string, string)[]
        {
            ("Download model", null),
            ("Mistral 7B Instruct v0.2 (medium, best overall)", "https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/resolve/main/mistral-7b-instruct-v0.2.Q4_K_M.gguf?download=true"),
            ("OpenHermes 2.5 7B (medium, best for conversation)", "https://huggingface.co/TheBloke/OpenHermes-2.5-Mistral-7B-GGUF/resolve/main/openhermes-2.5-mistral-7b.Q4_K_M.gguf?download=true"),
            ("Phi 2 (small, decent)", "https://huggingface.co/TheBloke/phi-2-GGUF/resolve/main/phi-2.Q4_K_M.gguf?download=true"),
        };
        public int SelectedModel = 0;
        private static readonly string serverZipUrl = "https://github.com/Mozilla-Ocho/llamafile/releases/download/0.6.2/llamafile-0.6.2.zip";
        private static readonly string server = LLMUnitySetup.GetAssetPath("llamafile-0.6.2.exe");
        private static readonly string apeARMUrl = "https://cosmo.zip/pub/cosmos/bin/ape-arm64.elf";
        private static readonly string apeARM = LLMUnitySetup.GetAssetPath("ape-arm64.elf");
        private static readonly string apeX86_64Url = "https://cosmo.zip/pub/cosmos/bin/ape-x86_64.elf";
        private static readonly string apeX86_64 = LLMUnitySetup.GetAssetPath("ape-x86_64.elf");

        [HideInInspector] public static float binariesProgress = 1;
        [HideInInspector] public float modelProgress = 1;
        [HideInInspector] public float modelCopyProgress = 1;
        [HideInInspector] public bool modelHide = true;
        private static float binariesDone = 0;
        private Process process;
        private bool mmapCrash = false;
        private ManualResetEvent serverBlock = new ManualResetEvent(false);
        static object crashKillLock = new object();
        static bool crashKill = false;
        /// \endcond

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static async Task InitializeOnLoad()
        {
            // Perform download when the build is finished
            await SetupBinaries();
        }

        private static async Task SetupBinaries()
        {
            if (binariesProgress < 1) return;
            binariesProgress = 0;
            binariesDone = 0;
            if (!File.Exists(apeARM)) await LLMUnitySetup.DownloadFile(apeARMUrl, apeARM, false, true, null, SetBinariesProgress);
            binariesDone += 1;
            if (!File.Exists(apeX86_64)) await LLMUnitySetup.DownloadFile(apeX86_64Url, apeX86_64, false, true, null, SetBinariesProgress);
            binariesDone += 1;
            if (!File.Exists(server))
            {
                string serverZip = Path.Combine(Application.temporaryCachePath, "llamafile.zip");
                await LLMUnitySetup.DownloadFile(serverZipUrl, serverZip, true, false, null, SetBinariesProgress);
                binariesDone += 1;

                using (ZipArchive archive = ZipFile.OpenRead(serverZip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Name == "llamafile")
                        {
                            AssetDatabase.StartAssetEditing();
                            entry.ExtractToFile(server, true);
                            AssetDatabase.StopAssetEditing();
                            break;
                        }
                    }
                }
                File.Delete(serverZip);
                binariesDone += 1;
            }
            binariesProgress = 1;
        }

        static void SetBinariesProgress(float progress)
        {
            binariesProgress = binariesDone / 4f + 1f / 4f * progress;
        }

        /// \cond HIDE
        public void DownloadModel(int optionIndex)
        {
            // download default model and disable model editor properties until the model is set
            SelectedModel = optionIndex;
            string modelUrl = modelOptions[optionIndex].Item2;
            if (modelUrl == null) return;
            modelProgress = 0;
            string modelName = Path.GetFileName(modelUrl).Split("?")[0];
            string modelPath = LLMUnitySetup.GetAssetPath(modelName);
            Task downloadTask = LLMUnitySetup.DownloadFile(modelUrl, modelPath, false, false, SetModel, SetModelProgress);
        }

        /// \endcond

        void SetModelProgress(float progress)
        {
            modelProgress = progress;
        }

        /// <summary>
        /// Allows to set the model used by the LLM.
        /// The model provided is copied to the Assets/StreamingAssets folder that allows it to also work in the build.
        /// Models supported are in .gguf format.
        /// </summary>
        /// <param name="path">path to model to use (.gguf format)</param>
        public async Task SetModel(string path)
        {
            // set the model and enable the model editor properties
            modelCopyProgress = 0;
            model = await LLMUnitySetup.AddAsset(path, LLMUnitySetup.GetAssetPath());
            SetTemplate(ChatTemplate.FromGGUF(path));
            EditorUtility.SetDirty(this);
            modelCopyProgress = 1;
        }

        public override void SetTemplate(string templateName)
        {
            base.SetTemplate(templateName);
            List<LLMClient> list = GetListeningClients();
            for (int i = 0; i < list.Count; i++)
            {
                LLMClient client = list[i];
                client.SetTemplate(templateName);
            }
        }

        /// <summary>
        /// Allows to set a LORA model to use in the LLM.
        /// The model provided is copied to the Assets/StreamingAssets folder that allows it to also work in the build.
        /// Models supported are in .bin format.
        /// </summary>
        /// <param name="path">path to LORA model to use (.bin format)</param>
        public async Task SetLora(string path)
        {
            // set the lora and enable the model editor properties
            modelCopyProgress = 0;
            lora = await LLMUnitySetup.AddAsset(path, LLMUnitySetup.GetAssetPath());
            EditorUtility.SetDirty(this);
            modelCopyProgress = 1;
        }

#endif

        List<LLMClient> GetListeningClients()
        {
            List<LLMClient> clients = new List<LLMClient>();
            LLMClient[] array = FindObjectsOfType<LLMClient>();
            for (int i = 0; i < array.Length; i++)
            {
                LLMClient client = array[i];
                if (client.GetType() == typeof(LLM)) continue;
                if (client.host == host && client.port == port)
                {
                    clients.Add(client);
                }
            }
            return clients;
        }

        void KillServersAfterUnityCrash()
        {
            lock (crashKillLock) {
                if (crashKill) return;
                LLMUnitySetup.KillServerAfterUnityCrash(server);
                crashKill = true;
            }
        }

        /// <summary>
        /// The Unity Awake function that initializes the state before the application starts.
        /// The following actions are executed:
        /// - existing servers are killed (if killExistingServersOnStart=true)
        /// - the LLM server is started (async if asynchronousStartup, synchronous otherwise)
        /// Additionally the Awake of the LLMClient is called to initialise the client part of the LLM object.
        /// </summary>
        new public IEnumerator Start()
        {
            if (killExistingServersOnStart) KillServersAfterUnityCrash();
            if (asynchronousStartup)
            {
                yield return StartCoroutine(StartLLMServer());
            }
            else _ = StartLLMServer();
            base.Awake();
            serverStarted = true;
        }

        private string SelectApeBinary()
        {
            // select the corresponding APE binary for the system architecture
            string arch = LLMUnitySetup.RunProcess("uname", "-m");
            // Debug.Log($"architecture: {arch}");
            string apeExe;
            if (arch.Contains("arm64") || arch.Contains("aarch64"))
            {
                apeExe = apeARM;
            }
            else
            {
                apeExe = apeX86_64;
                // if (!arch.Contains("x86_64"))
                //     Debug.Log($"Unknown architecture of processor {arch}! Falling back to x86_64");
            }
            return apeExe;
        }

        private void DebugLog(string message, bool logError = false)
        {
            // Debug log if debug is enabled
            if (!debug || message == null) return;
            if (logError) Debug.LogError(message);
            else Debug.Log(message);
        }

        private void ProcessError(string message)
        {
            // Debug log errors if debug is enabled
            DebugLog(message, true);
            if (message.Contains("assert(!isnan(x)) failed"))
            {
                mmapCrash = true;
            }
        }

        private void CheckIfListening(string message)
        {
            // Read the output of the llm binary and check if the server has been started and listening
            DebugLog(message);
            if (serverListening) return;
            try
            {
                ServerStatus status = JsonUtility.FromJson<ServerStatus>(message);
                if (status.message == "model loaded")
                {
                    // Debug.Log("LLM Server started!");
                    serverListening = true;
                    serverBlock.Set();
                }
            }
            catch {}
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            serverListening = false;
            serverBlock.Set();
        }

        private string EscapeSpaces(string input)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                return input.Replace(" ", "\" \"");
            if (input.Contains(" "))
                return $"'{input}'";
            return input;
        }

        private void RunServerCommand(string exe, string args)
        {
            string binary = exe;
            string arguments = args;
            if (Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer)
            {
                arguments = $"{EscapeSpaces(binary)} {arguments}";
                binary = SelectApeBinary();
                LLMUnitySetup.makeExecutable(binary);
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                arguments = $"-c \"{EscapeSpaces(binary)} {arguments}\"";
                binary = "sh";
            }
            // Debug.Log($"Server command: {binary} {arguments}");
            process = LLMUnitySetup.CreateProcess(binary, arguments, CheckIfListening, ProcessError, ProcessExited);
        }

        private IEnumerator StartLLMServer()
        {
            bool portInUse = true;
            if (asynchronousStartup)
            {
                yield return StartCoroutine(IsServerReachableCoroutine(5, result => portInUse = result));
            }
            else
            {
                portInUse = IsServerReachable();
            }
            if (portInUse)
            {
                Debug.LogError($"Port {port} is already in use, please use another port or kill all llamafile processes using it!");
                yield break;
            }

            // Start the LLM server in a cross-platform way
            if (model == "")
            {
                Debug.LogError("No model file provided!");
                yield break;
            }
            string modelPath = LLMUnitySetup.GetAssetPath(model);
            if (!File.Exists(modelPath))
            {
                Debug.LogError($"File {modelPath} not found!");
                yield break;
            }

            string loraPath = "";
            if (lora != "")
            {
                loraPath = LLMUnitySetup.GetAssetPath(lora);
                if (!File.Exists(loraPath))
                {
                    Debug.LogError($"File {loraPath} not found!");
                    yield break;
                }
            }

            int slots = parallelPrompts == -1 ? GetListeningClients().Count + 1 : parallelPrompts;
            string arguments = $" --port {port} -m {EscapeSpaces(modelPath)} -c {contextSize} -b {batchSize} --log-disable --nobrowser -np {slots}";
            if (remote) arguments += $" --host 0.0.0.0";
            if (numThreads > 0) arguments += $" -t {numThreads}";
            if (loraPath != "") arguments += $" --lora {EscapeSpaces(loraPath)}";

            string noGPUArgument = " -ngl 0 --gpu no";
            string GPUArgument = numGPULayers <= 0 ? noGPUArgument : $" -ngl {numGPULayers}";
            LLMUnitySetup.makeExecutable(server);

            RunServerCommand(server, arguments + GPUArgument);


            if (asynchronousStartup)
            {
                yield return StartCoroutine(WaitOneCoroutine(
                    serverBlock,
                    TimeSpan.FromSeconds(60),
                    onComplete: () => Debug.Log("Server started."),
                    onTimeout: () => Debug.LogError("Server startup timed out!")
                ));
            }
            else
            {
                serverBlock.WaitOne(60000); // Блокирующий вызов, если не используется асинхронный старт
            }

            if (process.HasExited && mmapCrash)
            {
                Debug.Log("Mmap error, fallback to no mmap use");
                serverBlock.Reset();
                arguments += " --no-mmap";

                RunServerCommand(server, arguments + GPUArgument);
                if (asynchronousStartup)
                {
                    yield return StartCoroutine(WaitOneCoroutine(
                        serverBlock,
                        TimeSpan.FromSeconds(60),
                        onComplete: () => Debug.Log("Server restarted with --no-mmap."),
                        onTimeout: () => Debug.LogError("Restart timed out!")
                    ));
                }
                else
                {
                    serverBlock.WaitOne(60000);
                }
            }

            if (process.HasExited && numGPULayers > 0)
            {
                Debug.Log("GPU failed, fallback to CPU");
                serverBlock.Reset();

                RunServerCommand(server, arguments + noGPUArgument);
                if (asynchronousStartup)
                {
                    yield return StartCoroutine(WaitOneCoroutine(
                        serverBlock,
                        TimeSpan.FromSeconds(60),
                        onComplete: () => Debug.Log("Server restarted with CPU."),
                        onTimeout: () => Debug.LogError("Restart with CPU timed out!")
                    ));
                }
                else
                {
                    serverBlock.WaitOne(60000);
                }
            }

            if (process.HasExited)
            {
                Debug.LogError("Server could not be started!");
            }
            else
            {
                LLMUnitySetup.SaveServerPID(process.Id);
            }
        }

        public void StopProcess()
        {
            if (process != null)
            {
                int pid = process.Id;
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit();
                }
                LLMUnitySetup.DeleteServerPID(pid);
            }
        }

        public void OnDestroy()
        {
            StopProcess();
        }

        public IEnumerator WaitOneCoroutine(WaitHandle handle, TimeSpan timeout, Action onComplete, Action onTimeout)
        {
            bool isSignaled = false;

            RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(
                handle, (state, timedOut) =>
                {
                    if (timedOut)
                        onTimeout?.Invoke();
                    else
                    {
                        isSignaled = true;
                        onComplete?.Invoke();
                    }
                },
                null,
                timeout,
                executeOnlyOnce: true
            );

            while (!isSignaled && handle.WaitOne(0) == false)
            {
                yield return null;
            }

            registration.Unregister(null);
        }
    }
}
