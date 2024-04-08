using UnityEngine;
using System.Collections;

using TheRavine.Base;
using TheRavine.Extentions;
public class AudioNatureController : MonoBehaviour
{
    [SerializeField] private AudioSource[] audioSource;
    [SerializeField] private AudioClip[] audioClipday, audioClipnight;
    [SerializeField] private AudioSource OSTSource;
    [SerializeField] private AudioClip[] OSTClip;
    [SerializeField] private float volumeOSTlimit, volumeNatureLimit;
    private float speedFade = 0.002f, lenghtOST, lenghtBack;

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
            if (audioSource[to].volume >= volumeNatureLimit)
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
                audioSource[0].clip = DayCycle.isday ? audioClipday[RavineRandom.RangeInt(0, audioClipday.Length)] : audioClipnight[RavineRandom.RangeInt(0, audioClipnight.Length)];
                audioSource[0].Play();
                lenghtBack = audioSource[0].clip.length;
                yield return StartCoroutine(ChangeAudio(1, 0));
            }
            else
            {
                audioSource[1].clip = DayCycle.isday ? audioClipday[RavineRandom.RangeInt(0, audioClipday.Length)] : audioClipnight[RavineRandom.RangeInt(0, audioClipnight.Length)];
                audioSource[1].Play();
                lenghtBack = audioSource[1].clip.length;
                yield return StartCoroutine(ChangeAudio(0, 1));
            }
            yield return new WaitForSeconds(lenghtBack - RavineRandom.RangeInt((int)lenghtBack / 8, (int)lenghtBack / 2));
        }
    }

    private IEnumerator OSTController()
    {
        while (true)
        {
            OSTSource.Stop();
            yield return new WaitForSeconds(RavineRandom.RangeInt(30, 100));
            yield return StartCoroutine(PlayOST(RavineRandom.RangeInt(0, OSTClip.Length)));
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
            if (OSTSource.volume >= volumeOSTlimit)
                change = false;
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(lenghtOST - RavineRandom.RangeInt((int)lenghtOST / 8, (int)lenghtOST / 2));

        while (!change)
        {
            OSTSource.volume -= speedFade;
            if (OSTSource.volume <= 0)
                yield break;
            yield return new WaitForEndOfFrame();
        }
    }
}