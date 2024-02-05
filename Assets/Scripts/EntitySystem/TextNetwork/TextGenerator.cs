using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class TextGenerator
{
    private Dictionary<string, List<KeyValuePair<string, int>>> bigrams;
    private Dictionary<string, HashSet<string>> precedingWords;
    private System.Random random = new System.Random();
    private int distortion;

    public TextGenerator(int _distortion)
    {
        bigrams = new Dictionary<string, List<KeyValuePair<string, int>>>();
        precedingWords = new Dictionary<string, HashSet<string>>();
        distortion = _distortion;
    }

    public void Train(string[] words)
    {
        for (int i = 0; i < words.Length - 1; i++)
        {
            string firstWord = words[i];
            string secondWord = words[i + 1];
            if (!bigrams.ContainsKey(firstWord))
                bigrams[firstWord] = new List<KeyValuePair<string, int>>();

            List<KeyValuePair<string, int>> bigramsList = bigrams[firstWord];
            bool est = false;
            for (int j = 0; j < bigramsList.Count; j++)
                if (secondWord == bigramsList[j].Key)
                    est = true;
            if (!est)
            {
                bigramsList.Add(new KeyValuePair<string, int>(secondWord, 1));
                continue;
            }

            for (int j = 0; j < bigramsList.Count; j++)
                if (bigramsList[j].Key == secondWord)
                {
                    bigramsList[j] = new KeyValuePair<string, int>(secondWord, bigramsList[j].Value + 1);
                    break;
                }

            if (!precedingWords.ContainsKey(secondWord))
                precedingWords[secondWord] = new HashSet<string>();
            precedingWords[secondWord].Add(firstWord);
        }

        foreach (var list in bigrams)
        {
            for (int j = 0; j < list.Value.Count; j++)
                if (list.Value[j].Value < 2 && list.Value.Count > 2)
                {
                    list.Value.RemoveAt(j);
                    j--;
                }
        }
    }

    public string GenerateSentenceWithWords(int length, List<string> requiredWords, int minDistanceBetweenRequiredWords)
    {
        List<string> sentenceParts = new List<string>();
        HashSet<string> usedWords = new HashSet<string>();

        // Распределение обязательных слов с учетом минимального расстояния
        for (int i = 0; i < requiredWords.Count; i++)
        {
            string word = requiredWords[i];
            // Добавляем пустые строки как заполнители для минимального расстояния между обязательными словами, если это не первое слово
            if (i > 0)
            {
                int placeholdersToAdd = Math.Min(minDistanceBetweenRequiredWords - 1, length - sentenceParts.Count - 1);
                sentenceParts.AddRange(Enumerable.Repeat("", placeholdersToAdd));
            }
            sentenceParts.Add(word);
            usedWords.Add(word);
            // Проверяем, достигнута ли желаемая длина предложения
            if (sentenceParts.Count >= length) break;
        }

        // Заполнение промежутков между обязательными словами, если осталось место
        for (int i = 0; i < sentenceParts.Count && sentenceParts.Count < length; i++)
        {
            if (string.IsNullOrEmpty(sentenceParts[i]))
            {
                string prevWord = i > 0 ? sentenceParts[i - 1] : null;
                string nextWord = GetNextWord(prevWord, usedWords);
                sentenceParts[i] = nextWord ?? ""; // Если не найдено подходящее слово, оставляем пустым
                usedWords.Add(nextWord);
            }
        }

        // Убираем пустые места, если они остались в конце предложения
        sentenceParts = sentenceParts.Where(part => !string.IsNullOrEmpty(part)).ToList();

        return string.Join(" ", sentenceParts);
    }


    private string GetNextWord(string prevWord, HashSet<string> usedWords)
    {
        if (prevWord == null || !bigrams.ContainsKey(prevWord)) return null;

        var possibleNextWords = bigrams[prevWord].Where(pair => !usedWords.Contains(pair.Key)).ToList();
        if (possibleNextWords.Count == 0) return null;

        int totalWeight = possibleNextWords.Sum(pair => pair.Value);
        int randomNumber = random.Next(totalWeight);
        int cumulative = 0;

        foreach (var pair in possibleNextWords)
        {
            cumulative += pair.Value;
            if (randomNumber < cumulative)
            {
                return pair.Key;
            }
        }

        return null;
    }


    public Dictionary<string, List<KeyValuePair<string, int>>> GetBigrams() => bigrams;
    public Dictionary<string, HashSet<string>> GetPreceding() => precedingWords;

    public void SetBigrams(Dictionary<string, List<KeyValuePair<string, int>>> loadedBigrams) => bigrams = loadedBigrams;
    public void SetPreceding(Dictionary<string, HashSet<string>> loadedPreceding) => precedingWords = loadedPreceding;
}