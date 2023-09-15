using UnityEngine;
using System.Collections;

public class AudioRadioController : MonoBehaviour
{

    [SerializeField] private AudioSource audioSource;
    private AudioClip[] audioClipRadio;
    [SerializeField] private AudioClip[] audioClipRadioSad;
    [SerializeField] private AudioClip[] audioClipRadioNormal;
    [SerializeField] private AudioClip[] audioClipRadioFunny;
    [SerializeField] private AudioClip[] audioStray;
    private bool[] playingYet;
    private int number;
    private int count;
    private int mood;

    private void Start()
    {
        DayCycle.newDay += ChangeMood;
        ChangeMood();
        StartCoroutine(Audio());
    }

    private void ChangeMood()
    {
        mood = UnityEngine.Random.Range(1, 3);
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
    }

    private IEnumerator Audio()
    {
        while (true)
        {
            number = Random.Range(0, audioClipRadio.Length);
            if (playingYet[number])
            {
                int firstnumber = number;
                for (int i = 0; i < audioClipRadio.Length; i++)
                {
                    if (!playingYet[i])
                    {
                        number = i;
                        count++;
                        break;
                    }
                }
                if (number == firstnumber)
                {
                    ChangeMood();
                }
            }
            // print(number);
            playingYet[number] = true;
            int audioLength = (int)audioClipRadio[number].length;
            audioSource.clip = audioClipRadio[number];

            audioSource.Play();
            yield return new WaitForSeconds(Random.Range(60, audioLength - 20));
            audioSource.Stop();
            audioSource.clip = audioStray[Random.Range(0, audioStray.Length)];
            audioSource.Play();
            yield return new WaitForSeconds(Random.Range(3, 5));
            audioSource.Stop();
        }
    }
}