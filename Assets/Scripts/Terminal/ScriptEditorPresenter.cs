using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;

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

        private void Start()
        {
            RefreshFilesList();
            
            // Редактор изначально выключен
            if (editorPanel != null)
                editorPanel.SetActive(false);
        }

        public void Initialize(CommandContext context, RiveInterpreter.TerminalCommandDelegate terminalCommandDelegate)
        {
            interpreter = new RiveInterpreter();
            
            terminalContext = context;
            interpreter.Initialize(terminalCommandDelegate);
        }

        public void CreateNewFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning("Имя файла не может быть пустым");
                return;
            }

            if (ScriptFileManager.FileExists(fileName))
            {
                Debug.LogWarning($"Файл {fileName} уже существует");
                return;
            }

            currentFileName = fileName;
            editorInputField.text = "";
            RefreshFilesList();
            
            Debug.Log($"Создан новый файл: {fileName}");
        }

        public void LoadFile(string fileName)
        {
            if (!ScriptFileManager.FileExists(fileName))
            {
                Debug.LogWarning($"Файл {fileName} не найден");
                return;
            }

            var content = ScriptFileManager.LoadFile(fileName);
            currentFileName = fileName;
            editorInputField.text = content ?? "";
            
            // Загружаем файл в интерпретатор
            interpreter.LoadFile(fileName, content);
            
            Debug.Log($"Загружен файл: {fileName}");
        }

        public void SaveCurrentFile()
        {
            if (string.IsNullOrEmpty(currentFileName))
            {
                Debug.LogWarning("Нет открытого файла для сохранения");
                return;
            }

            var content = editorInputField.text;
            ScriptFileManager.SaveFile(currentFileName, content);
            
            // Перезагружаем файл в интерпретатор
            interpreter.LoadFile(currentFileName, content);
            
            RefreshFilesList();
            Debug.Log($"Сохранен файл: {currentFileName}");
        }

        public void DeleteFile(string fileName)
        {
            ScriptFileManager.DeleteFile(fileName);
            interpreter.UnloadFile(fileName);
            
            if (currentFileName == fileName)
            {
                currentFileName = "";
                editorInputField.text = "";
            }
            
            RefreshFilesList();
            Debug.Log($"Удален файл: {fileName}");
        }

        public void ClearEditor()
        {
            editorInputField.text = "";
            currentFileName = "";
        }

        private void RefreshFilesList()
        {
            if (filesDropdown == null) return;

            filesDropdown.options.Clear();
            var files = ScriptFileManager.GetFilesList();
            
            files.ForEach(file => 
                filesDropdown.options.Add(new TMP_Dropdown.OptionData(file))
            );
            
            filesDropdown.RefreshShownValue();
        }

        public void OnDropdownValueChanged()
        {
            if (filesDropdown.value >= 0 && filesDropdown.value < filesDropdown.options.Count)
            {
                var selectedFile = filesDropdown.options[filesDropdown.value].text;
                LoadFile(selectedFile);
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
                var content = ScriptFileManager.LoadFile(fileName);
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

        public void LoadAllFilesToInterpreter()
        {
            var files = ScriptFileManager.GetFilesList();
            foreach (var fileName in files)
            {
                var content = ScriptFileManager.LoadFile(fileName);
                if (content != null)
                {
                    interpreter.LoadFile(fileName, content);
                }
            }
        }
    }
}