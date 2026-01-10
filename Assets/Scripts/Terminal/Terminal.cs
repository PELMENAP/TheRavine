using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using TMPro;

using TheRavine.Generator;
using TheRavine.EntityControl;

namespace TheRavine.Base
{
    public class Terminal : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI outputWindow;
        [SerializeField] private InputActionReference enterRef;
        [SerializeField] private GameObject terminalObject;
        [SerializeField] private Button confirmButton;
        [SerializeField] private GameObject graphyManager;
        [SerializeField] private ScriptEditorPresenter scriptEditor;
        [SerializeField] private RiveInputUI inputUI;
        private RiveRuntime interpreter;
        
        public CommandManager CommandManager { get; private set; }
        private CommandContext _context;
        private InputBindingAdapter _confirmBinding;
        private IRavineLogger logger;
        
        
        public void SetActiveTerminal()
        {
            bool newState = !terminalObject.activeSelf;
            terminalObject.SetActive(newState);
            
            if (newState)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        private void OnEnable()
        {
            _confirmBinding = InputBindingAdapter.Bind(confirmButton, enterRef, HandleEnter);
            
            inputField.onSubmit.AddListener(OnInputSubmit);
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        public async void Setup(IRavineLogger logger)
        {
            this.logger = logger;
            interpreter = new RiveRuntime(ExecuteTerminalCommandAsync, logger);
            CommandManager = new CommandManager();
            
            CommandManager.Register(
                new HelpCommand(),
                new PrintCommand(),
                new ClearCommand(),
                new TeleportCommand(),
                new SetValueCommand(),
                new RotateCommand(),
                new DebugCommand()
            );
            
            graphyManager.SetActive(false);
            _context = new CommandContext(outputWindow, CommandManager, null, graphyManager, scriptEditor, interpreter);

            if (scriptEditor != null)
            {   
                scriptEditor.Initialize(_context, interpreter, logger);
                inputUI.Initialize(interpreter, logger);

                scriptEditor.LoadAllFilesToInterpreter().Forget();
                
                CommandManager.Register(
                    new ExecuteScriptCommand(),
                    new EditorCommand(),
                    new EditFileCommand(),
                    new ScriptInfoCommand(),
                    new DeleteScriptCommand(),
                    new SaveScriptCommand(),
                    new NewScriptCommand(),
                    new CloseScriptCommand()
                );
            }

            await UniTask.CompletedTask;

            ProcessInput("~clear");
            WaitForDependencies().Forget();
        }

        private void RegisterInteractors()
        {
            interpreter.RegisterInteractor(new DigitalLockInteractor(347));
            
            interpreter.RegisterInteractor(new SequenceValidatorInteractor());
            
            interpreter.RegisterInteractor(new ChecksumInteractor());
            
            interpreter.RegisterInteractor(new CollatzInteractor());
        }


        private async UniTask<bool> ExecuteTerminalCommandAsync(string command)
        {
            try
            {
                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0].StartsWith("~"))
                {
                    if (CommandManager.TryGet(parts[0], out var cmd))
                    {
                        if (cmd is IValidatedCommand vc && !vc.Validate(_context))
                            return false;
                            
                        await cmd.ExecuteAsync(parts, _context);
                        return true;
                    }
                    else
                    {
                        _context.Display($"Неизвестная команда в скрипте: {parts[0]}");
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError($"Script terminal command error: {ex}");
                return false;
            }
        }

        private async UniTaskVoid WaitForDependencies()
        {   
            while (true)
            {
                try
                {
                    if(ServiceLocator.Services.TryGet(out MapGenerator generator))
                    {
                        _context.SetPlayers(ServiceLocator.Players.GetAllPlayers());
                        _context.SetGenerator(generator);
                        break;
                    }
                        
                    await UniTask.Delay(5000);
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error waiting for dependencies: {ex.Message}");
                    await UniTask.Delay(1000);
                }
            }
            logger.LogInfo($"Все службы Terminal инициализированны");
        }

        private void HandleEnter()
        {
            ProcessCurrentInput();
        }
        
        private void OnInputSubmit(string value)
        {
            ProcessCurrentInput();
        }
        
        private void OnInputEndEdit(string value)
        {
            if (terminalObject.activeSelf && !string.IsNullOrEmpty(value))
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        private void ProcessCurrentInput()
        {
            var raw = inputField.text;
            if (string.IsNullOrWhiteSpace(raw)) return;

            var input = raw.TrimEnd('\r', '\n');
            inputField.text = string.Empty;
            
            inputField.Select();
            inputField.ActivateInputField();

            ProcessInput(input);
        }

        private async void ProcessInput(string input)
        {
            try
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0].StartsWith("~"))
                {
                    if (CommandManager.TryGet(parts[0], out var cmd))
                    {
                        if (cmd is IValidatedCommand vc && !vc.Validate(_context))
                            return;
                            
                        await cmd.ExecuteAsync(parts, _context);
                    }
                    else
                    {
                        _context.Display($"Неизвестная команда: {parts[0]}");
                    }
                }
                else
                {
                    _context.Display(input);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Terminal command error: {ex}");
            }
        }

        public void Display(string message)
        {
            _context.Display(message);
        }
        private void OnDisable()
        {
            _confirmBinding?.Unbind();
            
            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(OnInputSubmit);
                inputField.onEndEdit.RemoveListener(OnInputEndEdit);
            }
        }
    }
}