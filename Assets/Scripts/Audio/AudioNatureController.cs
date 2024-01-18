using UnityEngine;
using System.Collections;

using TheRavine.Base;
public class AudioNatureController : MonoBehaviour
{
    [SerializeField] private AudioSource[] audioSource;
    [SerializeField] private AudioClip[] audioClipday, audioClipnight;
    [SerializeField] private AudioSource OSTSource;
    [SerializeField] private AudioClip[] OSTClip;
    private int currentOST;
    private float speedFade = 0.002f, lenghtOST;

    private void Start()
    {
        StartCoroutine(Audio());
        StartCoroutine(OSTController());
    }
    private IEnumerator ChangeAudio(int from, int to)
    {
        while (true)
        {
            audioSource[to].volume += speedFade;
            audioSource[from].volume -= speedFade;
            if (audioSource[from].volume <= 0.3)
            {
                audioSource[from].Stop();
                yield break;
            }
            yield return new WaitForEndOfFrame();
        }
    }

    private IEnumerator Audio()
    {
        while (true)
        {
            if (!audioSource[0].isPlaying)
            {
                audioSource[0].clip = DayCycle.isday ? audioClipday[Random.Range(0, audioClipday.Length)] : audioClipnight[Random.Range(0, audioClipnight.Length)];
                audioSource[0].Play();
                yield return StartCoroutine(ChangeAudio(1, 0));
            }
            else
            {
                audioSource[1].clip = DayCycle.isday ? audioClipday[Random.Range(0, audioClipday.Length)] : audioClipnight[Random.Range(0, audioClipnight.Length)];
                audioSource[1].Play();
                yield return StartCoroutine(ChangeAudio(0, 1));
            }
            yield return new WaitForSeconds(Random.Range(30, 120));
        }
    }

    private IEnumerator OSTController()
    {
        while (true)
        {
            OSTSource.Stop();
            yield return new WaitForSeconds(Random.Range(30, 100));
            currentOST = Random.Range(0, OSTClip.Length);
            yield return StartCoroutine(PlayOST(currentOST));
        }
    }
    private IEnumerator PlayOST(int current)
    {
        bool change = true;
        OSTSource.clip = OSTClip[current];
        lenghtOST = OSTSource.clip.length;
        OSTSource.Play();
        while (change)
        {
            OSTSource.volume += speedFade;
            if (OSTSource.volume >= 0.9)
                change = false;
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(lenghtOST - Random.Range(lenghtOST / 8, lenghtOST / 2));

        while (!change)
        {
            OSTSource.volume -= speedFade;
            if (OSTSource.volume <= 0)
                yield break;
            yield return new WaitForEndOfFrame();
        }
    }
}