using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using NaughtyAttributes;

namespace TheRavine.Base
{
    public class ScriptEditorPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_InputField editorInputField;
        [SerializeField] private TMP_Dropdown filesDropdown;
        [SerializeField] private GameObject editorPanel;

        private RiveRuntime interpreter;
        private string currentFileName = "";
        private CommandContext terminalContext;
        private ScriptFileManager scriptFileManager;
        private RavineLogger logger;

        public void Initialize(CommandContext context, RiveRuntime interpreter, RavineLogger logger)
        {
            this.interpreter = interpreter;
            this.logger = logger;
            terminalContext = context;

            scriptFileManager = new ScriptFileManager(new EncryptedPlayerPrefsStorage());

            editorPanel?.SetActive(false);

            filesDropdown.onValueChanged.AddListener(index => OnDropdownValueChanged());

            RefreshFilesList().Forget();
        }

        public async UniTaskVoid CreateNewFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                logger.LogWarning("Имя файла не может быть пустым");
                return;
            }

            if (RiveBuiltInFunctions.IsReserved(fileName))
            {
                logger.LogWarning($"Имя '{fileName}' зарезервировано");
                return;
            }

            bool isExist = await scriptFileManager.ExistsAsync(fileName);

            if (isExist)
            {
                logger.LogWarning($"Файл {fileName} уже существует");
                return;
            }

            currentFileName = fileName;
            editorInputField.text = "";
            RefreshFilesList().Forget();
            
            logger.LogInfo($"Создан новый файл: {fileName}");
        }

        public async UniTaskVoid LoadFile(string fileName)
        {
            if (RiveBuiltInFunctions.IsReserved(fileName))
            {
                logger.LogWarning($"Нельзя редактировать зарезервированное имя '{fileName}'");
                return;
            }

            bool isExist = await scriptFileManager.ExistsAsync(fileName);
            if (!isExist)
            {
                logger.LogWarning($"Файл {fileName} не найден");
                currentFileName = fileName;
                editorInputField.text = "";
                return;
            }

            var content = await scriptFileManager.LoadAsync(fileName);
            currentFileName = fileName;
            editorInputField.text = content ?? "";
            interpreter.LoadFile(fileName, content);
            RefreshFilesList().Forget();

            logger.LogInfo($"Загружен файл: {fileName}");
        }

        public void SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(currentFileName))
            {
                logger.LogWarning("Нет открытого файла для сохранения");
                return;
            }

            if (RiveBuiltInFunctions.IsReserved(currentFileName))
            {
                logger.LogWarning($"Нельзя сохранить файл с зарезервированным именем '{currentFileName}'");
                return;
            }

            var content = editorInputField.text;
            scriptFileManager.SaveAsync(currentFileName, content).Forget();
            
            interpreter.LoadFile(currentFileName, content);
            RefreshFilesList().Forget();
            logger.LogInfo($"Сохранен файл: {currentFileName}");
        }

        public async UniTaskVoid DeleteFile(string fileName)
        {
            if (RiveBuiltInFunctions.IsReserved(fileName))
            {
                logger.LogWarning($"Нельзя удалить зарезервированное имя '{fileName}'");
                return;
            }

            await scriptFileManager.DeleteAsync(fileName);
            interpreter.UnloadFile(fileName);
            
            if (currentFileName == fileName)
            {
                currentFileName = "";
                editorInputField.text = "";
            }
            
            RefreshFilesList().Forget();
            logger.LogInfo($"Удален файл: {fileName}");
        }

        public void ClearEditor()
        {
            editorInputField.text = "";
            currentFileName = "";
        }

        private static readonly string NoneName = "NONE";
        private async UniTaskVoid RefreshFilesList()
        {
            if (filesDropdown == null) return;

            filesDropdown.options.Clear();
            var files = await scriptFileManager.ListIdsAsync();

            filesDropdown.options.Add(new TMP_Dropdown.OptionData(NoneName));
            for (int i = 0; i < files.Count; i++)
            {
                filesDropdown.options.Add(new TMP_Dropdown.OptionData(files[i]));
                if (currentFileName == files[i]) filesDropdown.value = i + 1;
            }

            filesDropdown.RefreshShownValue();
        }

        public void OnDropdownValueChanged()
        {
            if (filesDropdown.value >= 0 && filesDropdown.value < filesDropdown.options.Count)
            {
                var selectedFile = filesDropdown.options[filesDropdown.value].text;
                LoadFile(selectedFile).Forget();
            }
        }

        public void SetEditorActive(bool active)
        {
            if (editorPanel != null)
            {
                editorPanel.SetActive(active);
                if (active && terminalContext != null)
                {
                    logger.LogInfo("Редактор скриптов активен");
                }
            }
        }

        public bool IsEditorActive()
        {
            return editorPanel != null && editorPanel.activeSelf;
        }

        public async UniTask<RiveRuntime.ScriptResult> ExecuteScriptAsync(string fileName, params int[] args)
        {
            if (!interpreter.IsFileLoaded(fileName) && !RiveBuiltInFunctions.IsBuiltIn(fileName))
            {
                var content = await scriptFileManager.LoadAsync(fileName);
                if (content != null)
                {
                    interpreter.LoadFile(fileName, content);
                }
                else
                {
                    return new RiveRuntime.ScriptResult
                    {
                        Success = false,
                        ErrorMessage = $"Файл {fileName} не найден"
                    };
                }
            }

            return await interpreter.ExecuteScriptAsync(fileName, args);
        }

        public async UniTaskVoid LoadAllFilesToInterpreter()
        {
            var files = await scriptFileManager.ListIdsAsync();
            foreach (var fileName in files)
            {
                var content = await scriptFileManager.LoadAsync(fileName);
                if (content != null)
                {
                    interpreter.LoadFile(fileName, content);
                }
            }
        }

        public string GetCurrentFileName() => currentFileName;
        public string GetCurrentContent() => editorInputField.text;
        public void LoadFileToInterpreter(string fileName, string content) => interpreter.LoadFile(fileName, content);
        public void UnloadFileFromInterpreter(string fileName) => interpreter.UnloadFile(fileName);
        public RiveRuntime.ProgramInfo GetFileInfo(string fileName) => interpreter.GetFileInfo(fileName);
        public IReadOnlyCollection<string> GetBuiltInFunctionNames() => interpreter.GetBuiltInFunctionNames();
        
        public void PushInput(int value) => interpreter.PushInput(value);
        public int GetWaitingInputReaders() => interpreter.GetWaitingInputReaders();
        public void RegisterInteractor(IInteractor interactor) => interpreter.RegisterInteractor(interactor);
        public void UnregisterInteractor(string name) => interpreter.UnregisterInteractor(name);
        public IReadOnlyCollection<string> GetRegisteredInteractors() => interpreter.GetRegisteredInteractors();
        public IInteractor GetInteractor(string name) => interpreter.GetInteractor(name);
    }
}