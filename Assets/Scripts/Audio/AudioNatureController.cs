using UnityEngine;
using System.Collections;

public class AudioNatureController : MonoBehaviour
{

    [SerializeField] private AudioSource[] audioSource;
    [SerializeField] private AudioClip[] audioClipday, audioClipnight;

    private bool isFade = false;
    private int audioSourceNext = 0;
    private float speedFade = 0.001f;

    private void Start()
    {
        for (int i = 0; i < audioSource.Length; i++)
        {
            if (!audioSource[i].isPlaying)
            {
                audioSource[i].clip = audioClipday[0];
                audioSource[i].Play();
                break;
            }
        }
        StartCoroutine(Audio());
    }
    private void Update()
    {
        if (isFade)
        {
            if (audioSourceNext == 0)
            {
                audioSource[0].volume += speedFade;
                audioSource[1].volume -= speedFade;
                if (audioSource[1].volume == 0)
                {
                    audioSource[1].Stop();
                }
            }
            else
            {
                audioSource[0].volume -= speedFade;
                audioSource[1].volume += speedFade;
                if (audioSource[0].volume == 0)
                {
                    audioSource[0].Stop();
                }
            }
        }
        if (audioSource[0].volume > 0.5f)
        {
            audioSource[0].volume = 0.5f;
        }
        if (audioSource[1].volume > 0.5f)
        {
            audioSource[1].volume = 0.5f;
        }
    }

    private IEnumerator Audio()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(30, 120));
            for (int i = 0; i < audioSource.Length; i++)
            {
                if (!audioSource[i].isPlaying)
                {
                    audioSource[i].clip = DayCycle.isday ? audioClipday[Random.Range(0, audioClipday.Length)] : audioClipnight[Random.Range(0, audioClipnight.Length)];
                    audioSource[i].Play();
                    audioSource[i].volume = 0;
                    isFade = true;
                    audioSourceNext = i;
                    break;
                }
            }
        }
    }
}