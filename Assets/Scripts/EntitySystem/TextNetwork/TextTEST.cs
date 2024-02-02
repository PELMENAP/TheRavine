using System.IO;
using System;
using System.Threading.Tasks;
using UnityEngine;
using NaughtyAttributes;
using Random = UnityEngine.Random;

using Accord.Neuro;
using Accord.Neuro.Learning;
using Accord.Neuro.Networks;
using Accord.Math.Random;

public class TextTEST : MonoBehaviour
{
    private TextGenerator textGenerator;
    string sampleText;
    public int textSize, distortion, maxZV;
    public string startSlovo, filePath;
    public string[] sentense;

    ActivationNetwork network;
    BackPropagationLearning teacher;

    double[][] inputs = new double[][]
        {
            new double[] { 0, 0 },
            new double[] { 1, 0 },
            new double[] { 0, 1 },
            new double[] { 1, 1 }
        };

    double[][] outputs = new double[][]
    {
            new double[] { 0 },
            new double[] { 1 },
            new double[] { 1 },
            new double[] { 0 }
    };


    void Start()
    {
        Generator.Seed = 0;

        network = new ActivationNetwork(new SigmoidFunction(), 2, 4, 1);

        teacher = new BackPropagationLearning(network)
        {
            LearningRate = 0.2,
            Momentum = 0.0
        };

        textGenerator = new TextGenerator(distortion);
        filePath = Path.Combine(Application.persistentDataPath, "bigrams.gz");
        // filePath = Path.Combine(Application.persistentDataPath, "bigrams.json");
    }

    [Button]
    private async void SaveBigrams()
    {
        await BigramsStorage.SaveBigramsAsync(textGenerator.GetBigrams(), filePath);
        // BigramsStorage.SaveBigrams(textGenerator.GetBigrams(), filePath);
    }

    [Button]
    private async void LoadBigrams()
    {
        var loadedBigrams = await BigramsStorage.LoadBigramsAsync(filePath);
        // var loadedBigrams = BigramsStorage.LoadBigrams(filePath);
        textGenerator.SetBigrams(loadedBigrams);
    }

    [Button]
    private void ReadTextFromFile()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("dostoevskiy");

        if (textAsset != null)
        {
            sampleText = textAsset.text;
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

    [Button]
    private void Generate()
    {
        Debug.Log(textGenerator.GenerateSentence(startSlovo, textSize));
    }

    [Button]
    private void GenerateRandom()
    {
        Debug.Log(textGenerator.GenerateSentence(Char.ToString(sampleText[Random.Range(0, sampleText.Length)]), textSize));
    }

    [Button]
    private void GenerateSentence()
    {
        Debug.Log(textGenerator.GenerateSentence(sentense, maxZV));
    }

    [Button]
    private void Network()
    {
        // Обучение
        for (int i = 0; i < 1000; i++)
        {
            double error = teacher.RunEpoch(inputs, outputs);
            Debug.Log($"Эпоха {i}, Ошибка: {error}");
        }

        // Тестирование
        foreach (var input in inputs)
        {
            double[] output = network.Compute(input);
            Debug.Log($"Вход: {input[0]}, {input[1]} -> Выход: {output[0]}");
        }
    }
}