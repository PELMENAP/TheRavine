using System.IO;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Random = UnityEngine.Random;
public class TextTEST : MonoBehaviour
{
    private TextGenerator textGenerator;
    string[] sampleText;
    public int textSize, distortion, maxZV, minDist;
    public string startSlovo, filePathBigrams, filePathPreceding;
    public List<string> sentense;

    void Start()
    {
        textGenerator = new TextGenerator(distortion);
        filePathBigrams = Path.Combine(Application.persistentDataPath, "bigrams.gz");
        filePathPreceding = Path.Combine(Application.persistentDataPath, "perceding.gz");
    }

    [Button]
    private async void SaveBigrams()
    {
        await BigramsStorage.SaveBigramsAsync(textGenerator.GetBigrams(), filePathBigrams);
        await BigramsStorage.SavePrecedingAsync(textGenerator.GetPreceding(), filePathPreceding);
    }

    [Button]
    private async void LoadBigrams()
    {
        var loadedBigrams = await BigramsStorage.LoadBigramsAsync(filePathBigrams);
        var loadedPreceding = await BigramsStorage.LoadPrecedingAsync(filePathPreceding);
        textGenerator.SetBigrams(loadedBigrams);
        textGenerator.SetPreceding(loadedPreceding);
    }

    [Button]
    private void ReadTextFromFile()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("dostoevskiy");

        if (textAsset != null)
        {
            sampleText = textAsset.text.Split(new char[] { ' ', '.', ',', ';', '!', '?' }, System.StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            Debug.LogError("Не удалось загрузить файл");
        }
    }

    [Button]
    private void Train()
    {
        textGenerator.Train(sampleText);
    }

    // [Button]
    // private void GenerateRandom()
    // {
    //     Debug.Log(textGenerator.GenerateSentence(sampleText[Random.Range(0, sampleText.Length)], textSize));
    // }

    [Button]
    private void GenerateSentence()
    {
        Debug.Log(textGenerator.GenerateSentenceWithWords(textSize, sentense, minDist));
    }

    // [Button]
    // private void GenerateBestSentence()
    // {
    //     Debug.Log(textGenerator.GenerateBestSentenceWithWords(textSize, sentense, minDist));
    // }
}