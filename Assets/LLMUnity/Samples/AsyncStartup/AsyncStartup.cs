using UnityEngine;
using LLMUnity;
using Cysharp.Threading.Tasks;
using TMPro;

namespace LLMUnitySamples
{
    public class AsyncStartup : MonoBehaviour
    {
        public TextMeshPro AIText;
        public GameObject LoadingScreen;
        public TextMeshPro LoadingText;
        private System.Action callback;
        private LLM llm;
        private void Start()
        {
            llm = LLMGetter.llmGetter.GetLLM();
            Loading().Forget();
        }

        private async UniTaskVoid Loading()
        {
            LoadingText.text = "Starting server...";
            LoadingScreen.gameObject.SetActive(true);
            await UniTask.Delay(3000);
            LoadingText.text = "Warming-up the model...";
            await UniTask.Delay(2000);
            LoadingScreen.gameObject.SetActive(false);
        }

        public void OnInputFieldSubmit(string message, System.Action _callback)
        {
            callback = _callback;
            AIText.text = "...";
            _ = llm.Chat(message, SetAIText, AIReplyComplete);
        }

        public void SetAIText(string text)
        {
            text += " ";
            string result = "";
            for(int i = 0; i < text.Length; i++) 
            {
                if(i + 6 < text.Length && text[i].ToString() + text[i+1].ToString() 
                + text[i+2].ToString() + text[i+3].ToString()
                + text[i+4].ToString() + text[i+5].ToString() == "<0x0A>") i += 6;
                result += text[i];
            }
            AIText.text = result;
        }

        public void AIReplyComplete()
        {
            callback?.Invoke();
            SetAIText(AIText.text);
        }

        public void CancelRequests()
        {
            llm.CancelRequests();
            AIReplyComplete();
        }
    }
}
