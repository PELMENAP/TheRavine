using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class BigramsStorage
{
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

    [System.Serializable]
    public class SerializablePrecedingArray
    {
        public SerializablePreceding[] Preceding;
    }

    [System.Serializable]
    public class SerializablePreceding
    {
        public string word;
        public List<string> nextWords;
    }

    public static async Task SavePrecedingAsync(Dictionary<string, HashSet<string>> bigrams, string filePath)
    {
        var serializablePreceding = bigrams.Select(pair => new SerializablePreceding
        {
            word = pair.Key,
            nextWords = pair.Value.ToList()
        }).ToList();

        SerializablePrecedingArray PrecedingArrayWrapper = new SerializablePrecedingArray { Preceding = serializablePreceding.ToArray() };

        string json = JsonUtility.ToJson(PrecedingArrayWrapper, true);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            await gzipStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
        }
    }

    public static async Task<Dictionary<string, HashSet<string>>> LoadPrecedingAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var memoryStream = new MemoryStream())
            {
                await gzipStream.CopyToAsync(memoryStream);
                string json = Encoding.UTF8.GetString(memoryStream.ToArray());
                SerializablePrecedingArray PrecedingArrayWrapper = JsonUtility.FromJson<SerializablePrecedingArray>(json);

                return PrecedingArrayWrapper.Preceding.ToDictionary(
                    b => b.word,
                    b => new HashSet<string>(b.nextWords)
                );
            }
        }

        return new Dictionary<string, HashSet<string>>();
    }
}