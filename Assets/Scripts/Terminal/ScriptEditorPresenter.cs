using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

namespace TheRavine.Base
{
    public class ScriptEditorPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_InputField editorInputField;
        [SerializeField] private TMP_Dropdown filesDropdown;
        [SerializeField] private GameObject editorPanel;
        
        private RiveInterpreter interpreter;
        private string currentFileName = "";
        private CommandContext terminalContext;
        private ScriptFileManager scriptFileManager;

        public void Initialize(CommandContext context, RiveInterpreter interpreter)
        {
            this.interpreter = interpreter;
            terminalContext = context;

            var encryptedPlayerPrefsStorage = new EncryptedPlayerPrefsStorage();
            scriptFileManager = new ScriptFileManager(encryptedPlayerPrefsStorage);

            if (editorPanel != null)
                editorPanel.SetActive(false);

            RefreshFilesList().Forget();
        }

        public async UniTaskVoid CreateNewFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning("Имя файла не может быть пустым");
                return;
            }

            bool isExist = await scriptFileManager.ExistsAsync(fileName);

            if (isExist)
            {
                Debug.LogWarning($"Файл {fileName} уже существует");
                return;
            }

            currentFileName = fileName;
            editorInputField.text = "";
            RefreshFilesList().Forget();
            
            Debug.Log($"Создан новый файл: {fileName}");
        }

        public async UniTaskVoid LoadFile(string fileName)
        {
            bool isExist = await scriptFileManager.ExistsAsync(fileName);
            if (!isExist)
            {
                Debug.LogWarning($"Файл {fileName} не найден");
                return;
            }

            var content = await scriptFileManager.LoadAsync(fileName);
            currentFileName = fileName;
            editorInputField.text = content ?? "";
            interpreter.LoadFile(fileName, content);
            
            Debug.Log($"Загружен файл: {fileName}");
        }

        public async UniTaskVoid SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(currentFileName))
            {
                Debug.LogWarning("Нет открытого файла для сохранения");
                return;
            }

            var content = editorInputField.text;
            await scriptFileManager.SaveAsync(currentFileName, content);
            
            interpreter.LoadFile(currentFileName, content);
            RefreshFilesList().Forget();
            Debug.Log($"Сохранен файл: {currentFileName}");
        }

        public async UniTaskVoid DeleteFile(string fileName)
        {
            await scriptFileManager.DeleteAsync(fileName);
            interpreter.UnloadFile(fileName);
            
            if (currentFileName == fileName)
            {
                currentFileName = "";
                editorInputField.text = "";
            }
            
            RefreshFilesList().Forget();
            Debug.Log($"Удален файл: {fileName}");
        }

        public void ClearEditor()
        {
            editorInputField.text = "";
            currentFileName = "";
        }

        private async UniTaskVoid RefreshFilesList()
        {
            if (filesDropdown == null) return;

            filesDropdown.options.Clear();
            var files = await scriptFileManager.ListIdsAsync();

            foreach (var file in files)
            {
                filesDropdown.options.Add(new TMP_Dropdown.OptionData(file));
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
                    terminalContext.Display("Редактор скриптов активен");
                }
            }
        }

        public bool IsEditorActive()
        {
            return editorPanel != null && editorPanel.activeSelf;
        }

        public async UniTask<RiveInterpreter.ScriptResult> ExecuteScriptAsync(string fileName, params int[] args)
        {
            if (!interpreter.IsFileLoaded(fileName))
            {
                var content = await scriptFileManager.LoadAsync(fileName);
                if (content != null)
                {
                    interpreter.LoadFile(fileName, content);
                }
                else
                {
                    return new RiveInterpreter.ScriptResult
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
        public RiveInterpreter.GameScriptFile GetFileInfo(string fileName) => interpreter.GetFileInfo(fileName);
    }
}