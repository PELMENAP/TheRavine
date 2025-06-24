using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public static class DelayedPerceptronStorage
{
    private static string GetSavePath(string fileName)
    {
        string directory = Path.Combine(Application.persistentDataPath, "PerceptronModels");
        
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        return Path.Combine(directory, $"{fileName}.bin");
    }
    public static async UniTask SaveAsync(DelayedPerceptron perceptron, string fileName, CancellationToken cancellationToken = default)
    {
        string path = GetSavePath(fileName);
        byte[] data = perceptron.Serialize();
        
        await UniTask.RunOnThreadPool(async () =>
        {
            try 
            {
                using FileStream fileStream = new FileStream(path, FileMode.Create);
                await fileStream.WriteAsync(data, 0, data.Length, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при сохранении модели: {ex.Message}");
                throw;
            }
        }, cancellationToken: cancellationToken);
        
        Debug.Log($"Модель сохранена в: {path}");
    }
    
    public static async UniTask<DelayedPerceptron> LoadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        string path = GetSavePath(fileName);
        
        if (!File.Exists(path))
        {
            Debug.LogError($"Файл модели не найден: {path}");
            return null;
        }
        
        byte[] data = null;
        
        await UniTask.RunOnThreadPool(async () =>
        {
            try
            {
                using FileStream fileStream = new FileStream(path, FileMode.Open);
                data = new byte[fileStream.Length];
                await fileStream.ReadAsync(data, 0, data.Length, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при загрузке модели: {ex.Message}");
                throw;
            }
        }, cancellationToken: cancellationToken);
        
        if (data != null)
        {
            Debug.Log($"Модель загружена из: {path}");
            return DelayedPerceptron.Deserialize(data);
        }
        
        return null;
    }
    public static bool Exists(string fileName)
    {
        string path = GetSavePath(fileName);
        return File.Exists(path);
    }
    public static List<string> GetAllSavedModels()
    {
        string directory = Path.Combine(Application.persistentDataPath, "PerceptronModels");
        
        if (!Directory.Exists(directory))
        {
            return new List<string>();
        }
        
        var files = Directory.GetFiles(directory, "*.bin");
        var fileNames = new List<string>();
        
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            fileNames.Add(name);
        }
        
        return fileNames;
    }
    public static bool Delete(string fileName)
    {
        string path = GetSavePath(fileName);
        
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при удалении модели: {ex.Message}");
            }
        }
        
        return false;
    }
}



public partial class DelayedPerceptron
{
    
    private const string DefaultSavePath = "PerceptronModels";
    public byte[] Serialize()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        
        int[] layerSizes = new int[_activations.Length];
        for (int i = 0; i < _activations.Length; i++)
        {
            layerSizes[i] = _activations[i].Length;
        }
        
        writer.Write(layerSizes.Length);
        foreach (int size in layerSizes)
        {
            writer.Write(size);
        }
        
        writer.Write(DelaySteps);
        
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            for (int neuron = 0; neuron < _weights[layer].Length; neuron++)
            {
                for (int input = 0; input < _weights[layer][neuron].Length; input++)
                {
                    writer.Write(_weights[layer][neuron][input]);
                }
            }
        }
        
        for (int layer = 0; layer < _biases.Length; layer++)
        {
            for (int neuron = 0; neuron < _biases[layer].Length; neuron++)
            {
                writer.Write(_biases[layer][neuron]);
            }
        }
        
        writer.Write(_delayedList.Count);
        foreach (var item in _delayedList)
        {
            writer.Write(item.Input.Length);
            foreach (float value in item.Input)
            {
                writer.Write(value);
            }
            
            writer.Write(item.Predicted);
            writer.Write(item.Evaluation);
        }
        
        return memoryStream.ToArray();
    }
    
    public static DelayedPerceptron Deserialize(byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        using var reader = new BinaryReader(memoryStream);
        
        int layersCount = reader.ReadInt32();
        int[] layerSizes = new int[layersCount];
        
        for (int i = 0; i < layersCount; i++)
        {
            layerSizes[i] = reader.ReadInt32();
        }
        
        int delaySteps = reader.ReadInt32();
        var perceptron = new DelayedPerceptron(
            layerSizes[0], 
            layerSizes[1], 
            layerSizes[2], 
            layerSizes[3], 
            delaySteps
        );
        
        for (int layer = 0; layer < perceptron._weights.Length; layer++)
        {
            for (int neuron = 0; neuron < perceptron._weights[layer].Length; neuron++)
            {
                for (int input = 0; input < perceptron._weights[layer][neuron].Length; input++)
                {
                    perceptron._weights[layer][neuron][input] = reader.ReadSingle();
                }
            }
        }
        
        for (int layer = 0; layer < perceptron._biases.Length; layer++)
        {
            for (int neuron = 0; neuron < perceptron._biases[layer].Length; neuron++)
            {
                perceptron._biases[layer][neuron] = reader.ReadSingle();
            }
        }
        
        int delayedCount = reader.ReadInt32();
        for (int i = 0; i < delayedCount; i++)
        {
            int inputSize = reader.ReadInt32();
            float[] input = new float[inputSize];
            
            for (int j = 0; j < inputSize; j++)
            {
                input[j] = reader.ReadSingle();
            }
            
            int predicted = reader.ReadInt32();
            float evaluation = reader.ReadSingle();
            var item = new DelayedItem(input, predicted);
            item.Evaluation = evaluation;
            perceptron._delayedList.Add(item);
        }
        
        return perceptron;
    }
}