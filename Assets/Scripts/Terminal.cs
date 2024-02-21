using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TMPro;

using TheRavine.Services;
using TheRavine.Generator;
using TheRavine.EntityControl;
namespace TheRavine.Base
{
    public class Terminal : MonoBehaviour, ISetAble
    {
        [SerializeField] private TextMeshProUGUI InputWindow;
        [SerializeField] private TextMeshProUGUI OutputWindow;
        [SerializeField] private InputActionReference EnterRef;
        public string input;
        private string[] words;
        private PlayerEntity playerData;
        private MapGenerator generator;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            playerData = locator.GetService<PlayerEntity>();
            generator = locator.GetService<MapGenerator>();
            EnterRef.action.performed += OnEnter;
            callback?.Invoke();
        }

        public void ShowSomething(string text)
        {
            OutputReaction(text);
        }
        private void OnEnter(InputAction.CallbackContext obj)
        {
            OutputWindow.text = "";
            input = InputWindow.text.Remove(InputWindow.text.Length - 1);
            ReadText(input);
            InputWindow.text = "";
        }
        private void ReadText(string input)
        {
            if (input.Length == 0)
                return;
            if (input[0] == '-')
            {
                words = input.Split(' ');
                try
                {
                    switch (words[0])
                    {
                        case "-tp":
                            switch (words[1])
                            {
                                case "i":
                                    TeleportCommandI();
                                    break;
                                default:
                                    OutputReaction("Неопределенный вид сущности");
                                    break;
                            }
                            break;
                        case "-set":
                            switch (words[1])
                            {
                                case "i":
                                    switch (words[2])
                                    {
                                        case "speed":
                                            SetPlayerValueCommand("speed");
                                            break;
                                        case "view":
                                            SetPlayerValueCommand("view");
                                            break;
                                        default:
                                            OutputReaction("Неизвестный параметр");
                                            break;
                                    }
                                    break;
                                default:
                                    OutputReaction("Неопределенный вид сущности");
                                    break;
                            }
                            break;
                        case "-когда":
                            OutputReaction("Спросите что-нибудь более оригинальное");
                            break;
                        case "-rotate":
                            switch (words[1])
                            {
                                case "90":
                                    RotateSpace(90);
                                    break;
                                case "-90":
                                    Debug.Log("-90");
                                    RotateSpace(-90);
                                    break;
                                default:
                                    OutputReaction("Неопределенная операция поворота");
                                    break;
                            }
                            break;
                        default:
                            OutputReaction("Неизвестная команда");
                            break;
                    }
                    // foreach (var item in words)
                    // {
                    //     print(item);
                    // }
                }
                catch
                {
                    OutputReaction("Недопустимый синтаксис");
                }
            }
            else
            {
                OutputReaction(input);
            }
        }

        private void TeleportCommandI()
        {
            int x, y;
            try
            {
                x = Convert.ToInt32(words[2]);
                y = Convert.ToInt32(words[3]);
            }
            catch
            {
                OutputReaction("Неизвестный тип координат");
                return;
            }
            if (Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000)
            {
                OutputReaction("Превышен лимит мира");
                return;
            }
            playerData.MoveTo(new Vector2(x, y));
            OutputReaction($"Выполнен телепорт на координаты: {x}, {y}");
        }

        private void SetPlayerValueCommand(string name)
        {
            int value;
            try
            {
                value = Convert.ToInt32(words[3]);
            }
            catch
            {
                OutputReaction("Неизвестный тип числа");
                return;
            }
            if (value < 0)
            {
                OutputReaction("Число не может быть отрицательным");
                return;
            }

            switch (name)
            {
                case "speed":
                    if (value > 100)
                    {
                        OutputReaction("Превышен лимит скорости");
                        return;
                    }
                    playerData.MOVEMENT_BASE_SPEED = value;
                    OutputReaction($"Скорость игрока: {value}");
                    break;
                case "view":
                    if (value > 30)
                    {
                        OutputReaction("Превышен лимит обзора");
                        return;
                    }
                    playerData.maxMouseDis = value;
                    OutputReaction($"Максимальный обзор игрока: {value}");
                    break;
                default:
                    OutputReaction("Неизвестный параметр");
                    break;
            }
        }
        private void RotateSpace(sbyte angle)
        {
            Debug.Log(angle);
            generator.RotateBasis(angle);
        }
        private void OutputReaction(string message)
        {
            OutputWindow.text = message;
            TerminalOutputFade(OutputWindow).Forget();
        }
        private async UniTaskVoid TerminalOutputFade(TextMeshProUGUI window)
        {
            // Color colorM = new Color(window.color.r, window.color.g, window.color.b, window.color.a);
            // for (int i = 255; i > 0; i--)
            // {
            //     print("change");
            //     window.color = new Color(i, i, i, i);
            //     await UniTask.Delay(50);
            // }
            await UniTask.Delay(5000);
            window.text = "";
            InputWindow.text = "";
            // window.color = colorM;
        }
        public void BreakUp()
        {
            EnterRef.action.performed -= OnEnter;
        }
    }
}