using LLMUnity;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class LLMGetter : MonoBehaviour
{
    [SerializeField] private LLM llm;
    public static LLMGetter llmGetter;

    private void Awake() {
        llmGetter = this;
        Loading().Forget();
    }

    private async UniTaskVoid Loading()
    {
        while (!llm.serverStarted)
        {
            await UniTask.Delay(1000);
        }
        _ = llm.Warmup();
    }

    public LLM GetLLM() => llm;
}
