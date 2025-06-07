using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TMPro;

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
        private string[] _words;
        public PlayerEntity playerData;
        private MapGenerator generator;
        public void SetUp(ISetAble.Callback callback)
        {
            playerData = ServiceLocator.GetService<PlayerModelView>().playerEntity;
            generator = ServiceLocator.GetService<MapGenerator>();
            EnterRef.action.performed += OnEnter;
            callback?.Invoke();
        }
        
        public void ShowSomething(string text)
        {
            Display(text);
        }
        public void OnEnter()
        {
            OutputWindow.text = "";
            input = InputWindow.text.Remove(InputWindow.text.Length - 1);
            ReadText(input);
            InputWindow.text = "";
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
                _words = input.Split(' ');
                try
                {
                    switch (_words[0])
                    {
                        case "-tp":
                            switch (_words[1])
                            {
                                case "i":
                                    TeleportCommandI();
                                    break;
                                default:
                                    Display("Неопределенный вид сущности");
                                    break;
                            }
                            break;
                        case "-set":
                            switch (_words[1])
                            {
                                case "i":
                                    switch (_words[2])
                                    {
                                        case "speed":
                                            SetPlayerValueCommand("speed");
                                            break;
                                        case "view":
                                            SetPlayerValueCommand("view");
                                            break;
                                        default:
                                            Display("Неизвестный параметр");
                                            break;
                                    }
                                    break;
                                default:
                                    Display("Неопределенный вид сущности");
                                    break;
                            }
                            break;
                        case "-когда":
                            Display("Спросите что-нибудь более оригинальное");
                            break;
                        case "-rotate":
                            switch (_words[1])
                            {
                                case "90":
                                    RotateSpace(90);
                                    break;
                                case "-90":
                                    Debug.Log("-90");
                                    RotateSpace(-90);
                                    break;
                                default:
                                    Display("Неопределенная операция поворота");
                                    break;
                            }
                            break;
                        default:
                            Display("Неизвестная команда");
                            break;
                    }
                    // foreach (var item in words)
                    // {
                    //     print(item);
                    // }
                }
                catch
                {
                    Display("Недопустимый синтаксис");
                }
            }
            else
            {
                Display(input);
            }
        }

        private void TeleportCommandI()
        {
            int x, y;
            try
            {
                x = Convert.ToInt32(_words[2]);
                y = Convert.ToInt32(_words[3]);
            }
            catch
            {
                Display("Неизвестный тип координат");
                return;
            }
            if (Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000)
            {
                Display("Превышен лимит мира");
                return;
            }
            playerData.GetEntityComponent<TransformComponent>().GetEntityTransform().position = new Vector2(x, y);
            Display($"Выполнен телепорт на координаты: {x}, {y}");
        }

        private void SetPlayerValueCommand(string name)
        {
            int value;
            try
            {
                value = Convert.ToInt32(_words[3]);
            }
            catch
            {
                Display("Неизвестный тип числа");
                return;
            }
            if (value < 0)
            {
                Display("Число не может быть отрицательным");
                return;
            }

            switch (name)
            {
                case "speed":
                    if (value > 100)
                    {
                        Display("Превышен лимит скорости");
                        return;
                    }
                    playerData.GetEntityComponent<MovementComponent>().baseStats.baseSpeed = value;
                    Display($"Скорость игрока: {value}");
                    break;
                case "view":
                    if (value > 30)
                    {
                        Display("Превышен лимит обзора");
                        return;
                    }
                    playerData.GetEntityComponent<AimComponent>().BaseStats.crosshairDistance = value;
                    Display($"Максимальный обзор игрока: {value}");
                    break;
                default:
                    Display("Неизвестный параметр");
                    break;
            }
        }
        private void RotateSpace(sbyte angle)
        {
            Debug.Log(angle);
            // generator.RotateBasis(angle);
        }
        public void Display(string message)
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
        public void BreakUp(ISetAble.Callback callback)
        {
            EnterRef.action.performed -= OnEnter;
            callback?.Invoke();
        }
    }
}