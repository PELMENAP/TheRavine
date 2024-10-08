using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
public class PlayerDialogOutput : MonoBehaviour
{
    [SerializeField] private TextMeshPro dialogText;
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] private float delayBetweenDialogs = 1.0f;
    private readonly Queue<string> dialogQueue = new Queue<string>();
    private void Awake()
    {
        OutputDialogs().Forget();
        dialogText.text = "";
    }
    public void AddAnswer(string dialog)
    {
        dialogQueue.Enqueue(dialog);
    }

    private async UniTaskVoid OutputDialogs()
    {
        while (true)
        {
            if (dialogQueue.Count > 0)
            {
                string dialogToOutput = dialogQueue.Dequeue();
                await TypeDialog(dialogToOutput);
            }
            await UniTask.Delay(System.TimeSpan.FromSeconds(delayBetweenDialogs));
        }
    }

    private async UniTask TypeDialog(string dialog)
    {
        foreach (char letter in dialog)
        {
            dialogText.text += letter;
            await UniTask.Delay(System.TimeSpan.FromSeconds(typingSpeed));
        }
        if (dialogQueue.Count == 0)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(delayBetweenDialogs * delayBetweenDialogs));
            dialogText.text = "";
        }
    }

    private void OnDestroy()
    {
        dialogQueue.Clear();
    }
}
