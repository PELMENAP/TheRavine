using UnityEngine;
using LLMUnity;
using System.Collections;
using TMPro;

namespace LLMUnitySamples
{
    public class AsyncStartup : MonoBehaviour
    {
        public LLM llm;
        public TMP_InputField playerText;
        public TextMeshPro AIText;
        public GameObject LoadingScreen;
        public TextMeshPro LoadingText;

        void Start()
        {
            StartCoroutine(Loading());
        }

        IEnumerator Loading()
        {
            LoadingText.text = "Starting server...";
            LoadingScreen.gameObject.SetActive(true);
            playerText.interactable = false;
            while (!llm.serverStarted)
            {
                yield return null;
            }
            LoadingText.text = "Warming-up the model...";
            _ = llm.Warmup(LoadingComplete);
        }

        void LoadingComplete()
        {
            playerText.interactable = true;
            LoadingScreen.gameObject.SetActive(false);
            playerText.Select();
        }

        public void OnInputFieldSubmit(string message)
        {
            playerText.interactable = false;
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
            SetAIText(AIText.text);
            playerText.interactable = true;
            playerText.Select();
            playerText.text = "";
        }

        public void CancelRequests()
        {
            llm.CancelRequests();
            AIReplyComplete();
        }
    }
}
