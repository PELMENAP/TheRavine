using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TMPro;

public class Terminal : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI InputWindow;
    [SerializeField] private TextMeshProUGUI OutputWindow;

    [SerializeField] private InputActionReference EnterRef;
    public string input;
    private void OnEnter(InputAction.CallbackContext obj)
    {
        OutputWindow.text = "";
        input = InputWindow.text.Remove(InputWindow.text.Length - 1);
        ReadText(input);

        InputWindow.text = "";
    }

    private void ReadText(string input)
    {
        if (input[0] == '-')
        {
            string[] words = input.Split(' ');
            try
            {
                switch (words[0])
                {
                    case "-tp":
                        switch (words[1])
                        {
                            case "i":
                                TeleportCommandI(words);
                                break;
                            default:
                                OutputReaction("неопределенный вид сущности");
                                break;
                        }
                        break;
                    default:
                        OutputReaction("неизвестная команда");
                        break;
                }
                // foreach (var item in words)
                // {
                //     print(item);
                // }
            }
            catch
            {
                OutputReaction("недопустимый синтаксис");
            }
        }
    }

    private void TeleportCommandI(string[] words)
    {
        int x, y;
        try
        {
            x = Convert.ToInt32(words[2]);
            y = Convert.ToInt32(words[3]);
        }
        catch
        {
            OutputReaction("неизвестный тип координат");
            return;
        }
        if (Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000)
        {
            OutputReaction("превышен лимит мира");
            return;
        }
        PlayerData.instance.MoveTo(new Vector2(x, y));
        OutputReaction($"выполнен телепорт на координаты: {x}, {y}");
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
    private void OnEnable()
    {
        EnterRef.action.performed += OnEnter;
    }
    private void OnDisable()
    {
        EnterRef.action.performed -= OnEnter;
    }
}
