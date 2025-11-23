using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public interface ISerializableNeuralModel
{
    byte[] Serialize();
}

public interface INeuralModelFactory<out T> where T : ISerializableNeuralModel
{
    T Deserialize(byte[] data);
}

public static class NeuralModelStorage
{
    private const string DefaultModelsDirectory = "NeuralModels";
    private static readonly Dictionary<Type, object> _factories = new Dictionary<Type, object>();
    
    public static void RegisterFactory<T>(INeuralModelFactory<T> factory) where T : ISerializableNeuralModel
    {
        _factories[typeof(T)] = factory;
    }
    
    private static string GetSavePath(string modelType, string fileName)
    {
        string directory = Path.Combine(Application.persistentDataPath, DefaultModelsDirectory, modelType);
        
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        return Path.Combine(directory, $"{fileName}.bin");
    }
    
    public static async UniTask SaveAsync<T>(T model, string fileName, CancellationToken cancellationToken = default) 
        where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string path = GetSavePath(modelType, fileName);
        byte[] data = model.Serialize();
        
        await UniTask.RunOnThreadPool(async () =>
        {
            try 
            {
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 
                    bufferSize: 4096, useAsync: true);
                await fileStream.WriteAsync(data, 0, data.Length, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при сохранении модели {modelType}: {ex.Message}");
                throw;
            }
        }, cancellationToken: cancellationToken);
        
        Debug.Log($"Модель {modelType} сохранена: {path}");
    }
    public static async UniTask<T> LoadAsync<T>(string fileName, CancellationToken cancellationToken = default) 
        where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string path = GetSavePath(modelType, fileName);
        
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Файл модели {modelType} не найден: {path}");
            return default;
        }
        
        if (!_factories.TryGetValue(typeof(T), out var factoryObj))
        {
            Debug.LogError($"Фабрика для типа {modelType} не зарегистрирована. Используйте RegisterFactory<{modelType}>()");
            return default;
        }
        
        var factory = (INeuralModelFactory<T>)factoryObj;
        byte[] data = null;
        
        await UniTask.RunOnThreadPool(async () =>
        {
            try
            {
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 
                    bufferSize: 4096, useAsync: true);
                data = new byte[fileStream.Length];
                await fileStream.ReadAsync(data, 0, data.Length, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при загрузке модели {modelType}: {ex.Message}");
                throw;
            }
        }, cancellationToken: cancellationToken);
        
        if (data != null)
        {
            Debug.Log($"Модель {modelType} загружена: {path}");
            return factory.Deserialize(data);
        }
        
        return default;
    }
    
    public static bool Exists<T>(string fileName) where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string path = GetSavePath(modelType, fileName);
        return File.Exists(path);
    }
    
    public static List<string> GetAllSavedModels<T>() where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string directory = Path.Combine(Application.persistentDataPath, DefaultModelsDirectory, modelType);
        
        if (!Directory.Exists(directory))
        {
            return new List<string>();
        }
        
        var files = Directory.GetFiles(directory, "*.bin");
        var fileNames = new List<string>(files.Length);
        
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            fileNames.Add(name);
        }
        
        return fileNames;
    }
    
    public static List<string> GetAllModelTypes()
    {
        string baseDirectory = Path.Combine(Application.persistentDataPath, DefaultModelsDirectory);
        
        if (!Directory.Exists(baseDirectory))
        {
            return new List<string>();
        }
        
        var directories = Directory.GetDirectories(baseDirectory);
        var modelTypes = new List<string>(directories.Length);
        
        foreach (var directory in directories)
        {
            string typeName = Path.GetFileName(directory);
            modelTypes.Add(typeName);
        }
        
        return modelTypes;
    }
    
    public static bool Delete<T>(string fileName) where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string path = GetSavePath(modelType, fileName);
        
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Debug.Log($"Модель {modelType} удалена: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ошибка при удалении модели {modelType}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Модель {modelType} не найдена для удаления: {fileName}");
        }
        
        return false;
    }
    
    public static async UniTask<int> DeleteAllModelsAsync<T>(CancellationToken cancellationToken = default) 
        where T : ISerializableNeuralModel
    {
        return await UniTask.RunOnThreadPool(() =>
        {
            string modelType = typeof(T).Name;
            string directory = Path.Combine(Application.persistentDataPath, DefaultModelsDirectory, modelType);
            
            if (!Directory.Exists(directory))
                return 0;
            
            var files = Directory.GetFiles(directory, "*.bin");
            int deletedCount = 0;
            
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Ошибка при удалении файла {file}: {ex.Message}");
                }
            }
            
            Debug.Log($"Удалено {deletedCount} моделей типа {modelType}");
            return deletedCount;
        }, cancellationToken: cancellationToken);
    }
    
    public static long GetModelSize<T>(string fileName) where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string path = GetSavePath(modelType, fileName);
        
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }
    
    public static ModelInfo GetModelInfo<T>(string fileName) where T : ISerializableNeuralModel
    {
        string modelType = typeof(T).Name;
        string path = GetSavePath(modelType, fileName);
        
        if (!File.Exists(path))
            return null;
        
        var fileInfo = new FileInfo(path);
        return new ModelInfo
        {
            Name = fileName,
            Type = modelType,
            Size = fileInfo.Length,
            CreatedTime = fileInfo.CreationTime,
            ModifiedTime = fileInfo.LastWriteTime
        };
    }
}