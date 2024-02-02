using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class SerializableBigramArray
{
    public SerializableBigram[] bigrams;
}

[System.Serializable]
public class SerializableBigram
{
    public string word;
    public List<SerializableNextWord> nextWords;

    [System.Serializable]
    public class SerializableNextWord
    {
        public string word;
        public int count;
    }
}

public class BigramsStorage
{
    // Асинхронное сохранение биграмм с сжатием
    public static async Task SaveBigramsAsync(Dictionary<string, List<KeyValuePair<string, int>>> bigrams, string filePath)
    {
        var serializableBigrams = bigrams.Select(pair => new SerializableBigram
        {
            word = pair.Key,
            nextWords = pair.Value.Select(kv => new SerializableBigram.SerializableNextWord { word = kv.Key, count = kv.Value }).ToList()
        }).ToList();

        SerializableBigramArray bigramArrayWrapper = new SerializableBigramArray { bigrams = serializableBigrams.ToArray() };

        string json = JsonUtility.ToJson(bigramArrayWrapper, true);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            await gzipStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
        }
    }

    // Асинхронная загрузка биграмм с распаковкой
    public static async Task<Dictionary<string, List<KeyValuePair<string, int>>>> LoadBigramsAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                await gzipStream.CopyToAsync(memoryStream);
                string json = Encoding.UTF8.GetString(memoryStream.ToArray());
                SerializableBigramArray bigramArrayWrapper = JsonUtility.FromJson<SerializableBigramArray>(json);

                return bigramArrayWrapper.bigrams.ToDictionary(
                    b => b.word,
                    b => b.nextWords.Select(n => new KeyValuePair<string, int>(n.word, n.count)).ToList()
                );
            }
        }

        return new Dictionary<string, List<KeyValuePair<string, int>>>();
    }

    public static void SaveBigrams(Dictionary<string, List<KeyValuePair<string, int>>> bigrams, string filePath)
    {
        var serializableBigrams = new List<SerializableBigram>();

        foreach (var pair in bigrams)
        {
            var serializableBigram = new SerializableBigram
            {
                word = pair.Key,
                nextWords = pair.Value.Select(kv => new SerializableBigram.SerializableNextWord { word = kv.Key, count = kv.Value }).ToList()
            };
            serializableBigrams.Add(serializableBigram);
        }
        SerializableBigramArray bigramArrayWrapper = new SerializableBigramArray { bigrams = serializableBigrams.ToArray() };

        string json = JsonUtility.ToJson(bigramArrayWrapper, true);
        File.WriteAllText(filePath, json);
    }

    public static Dictionary<string, List<KeyValuePair<string, int>>> LoadBigrams(string filePath)
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            SerializableBigramArray bigramArrayWrapper = JsonUtility.FromJson<SerializableBigramArray>(json);
            return bigramArrayWrapper.bigrams.ToDictionary(
                b => b.word,
                b => b.nextWords.Select(n => new KeyValuePair<string, int>(n.word, n.count)).ToList()
            );
        }

        return new Dictionary<string, List<KeyValuePair<string, int>>>();
    }
}