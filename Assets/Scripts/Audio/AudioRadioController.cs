using System.Threading;

using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Extensions;

public class AudioRadioController : MonoBehaviour
{
    private CancellationTokenSource _cts;
    [SerializeField] private AudioSource audioSource;
    private AudioClip[] audioClipRadio;
    [SerializeField] private AudioClip[] audioClipRadioSad;
    [SerializeField] private AudioClip[] audioClipRadioNormal;
    [SerializeField] private AudioClip[] audioClipRadioFunny;
    [SerializeField] private AudioClip[] audioStray;
    private bool[] playingYet;
    private int number;
    // private int count;
    private int mood;

    public void StartDefaultRadio()
    {
        _cts    = new CancellationTokenSource();
        ChangeMood().Forget();
        Audio().Forget();
    }

    public void StartWinRadio(AudioClip audioClip)
    {
        audioSource.clip = audioClip;
        audioSource.Play();
    }

    private async UniTaskVoid ChangeMood()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            mood = RavineRandom.RangeInt(1, 3);
            switch (mood)
            {
                case 1:
                    audioClipRadio = audioClipRadioSad;
                    break;
                case 2:
                    audioClipRadio = audioClipRadioNormal;
                    break;
                case 3:
                    audioClipRadio = audioClipRadioFunny;
                    break;
            }
            // print("Настроение: ");
            // print(mood);
            playingYet = new bool[audioClipRadio.Length];
            for (int j = 0; j < audioClipRadio.Length; j++)
            {
                playingYet[j] = false;
            }
            await UniTask.Delay(100000);
        }
    }

    private async UniTaskVoid Audio()
    {
        while (true)
        {
            number = RavineRandom.RangeInt(0, audioClipRadio.Length);
            if (playingYet[number])
            {
                int firstnumber = number;
                for (int i = 0; i < audioClipRadio.Length; i++)
                {
                    if (!playingYet[i])
                    {
                        number = i;
                        // count++;
                        break;
                    }
                }
                if (number == firstnumber)
                {
                    ChangeMood().Forget();
                }
            }
            // print(number);
            playingYet[number] = true;
            int audioLength = (int)audioClipRadio[number].length;
            audioSource.clip = audioClipRadio[number];

            audioSource.Play();
            await UniTask.Delay(1000 * RavineRandom.RangeInt(60, audioLength - 20));
            audioSource.Stop();
            audioSource.clip = audioStray[RavineRandom.RangeInt(0, audioStray.Length)];
            audioSource.Play();
            await UniTask.Delay(1000 * RavineRandom.RangeInt(3, 5));
            audioSource.Stop();
        }
    }

    private void OnDisable() {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}