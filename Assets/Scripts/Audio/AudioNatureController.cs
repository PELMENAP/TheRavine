using UnityEngine;
using System.Collections;

using TheRavine.Base;
using TheRavine.Extensions;
public class AudioNatureController : MonoBehaviour
{
    [SerializeField] private AudioSource[] audioSource;
    [SerializeField] private AudioClip[] audioClipday, audioClipnight;
    [SerializeField] private AudioSource OSTSource;
    [SerializeField] private AudioClip[] OSTClip;
    [SerializeField] private float volumeOSTlimit, volumeNatureLimit;
    private float speedFade = 0.002f, lengthOST, lengthBack;

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
            audioSource[from].volume -= speedFade * 2;
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
                lengthBack = audioSource[0].clip.length;
                yield return StartCoroutine(ChangeAudio(1, 0));
            }
            else
            {
                audioSource[1].clip = DayCycle.isday ? audioClipday[RavineRandom.RangeInt(0, audioClipday.Length)] : audioClipnight[RavineRandom.RangeInt(0, audioClipnight.Length)];
                audioSource[1].Play();
                lengthBack = audioSource[1].clip.length;
                yield return StartCoroutine(ChangeAudio(0, 1));
            }
            yield return new WaitForSeconds(lengthBack - RavineRandom.RangeInt((int)lengthBack / 8, (int)lengthBack / 2));
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
        lengthOST = OSTSource.clip.length;
        OSTSource.Play();
        while (change)
        {
            OSTSource.volume += speedFade;
            if (OSTSource.volume >= volumeOSTlimit)
                change = false;
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(lengthOST - RavineRandom.RangeInt((int)lengthOST / 8, (int)lengthOST / 2));

        while (!change)
        {
            OSTSource.volume -= speedFade;
            if (OSTSource.volume <= 0)
                yield break;
            yield return new WaitForEndOfFrame();
        }
    }
}