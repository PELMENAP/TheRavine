using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class TextGenerator
{
    private Dictionary<string, List<KeyValuePair<string, int>>> bigrams;

    private int distortion;

    public TextGenerator(int _distortion)
    {
        bigrams = new Dictionary<string, List<KeyValuePair<string, int>>>();
        distortion = _distortion;
    }

    public void Train(string words)
    {
        for (int i = 0; i < words.Length - 1; i++)
        {
            string firstWord = Char.ToString(words[i]);
            string secondWord = Char.ToString(words[i + 1]);
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

    public string GenerateSentence(string startingWord, int length)
    {
        string currentWord = startingWord;
        string sentence = currentWord;


        for (int i = 1; i < length; i++)
            if (bigrams.ContainsKey(currentWord))
            {
                currentWord = GetNextWord(currentWord);
                if (currentWord == " ")
                {
                    if (sentence[sentence.Length - 2] != ' ')
                        sentence += currentWord;
                }
                else
                    sentence += " " + currentWord;
                if (currentWord[currentWord.Length - 1] == '.')
                    break;
                Debug.Log(sentence);
            }
            else
                break;
        return sentence;
    }

    public string GenerateSentence(string[] words, int maxZV)
    {
        string sentence = "";
        for (int i = 0; i < words.Length; i++)
        {
            string currentWord = words[i];
            sentence += currentWord;
            int ZV = Random.Range(0, maxZV);
            for (int j = 0; j < ZV; j++)
            {
                currentWord = GetNextWord(Char.ToString(currentWord[currentWord.Length - 1]));
                sentence += currentWord;
            }
        }
        return sentence;
    }

    private string GetNextWord(string word)
    {
        List<KeyValuePair<string, int>> nextWords = bigrams[word];

        string answer = "";
        int weight = 0;
        for (int i = 0; i < distortion; i++)
        {
            KeyValuePair<string, int> current = nextWords[Random.Range(0, nextWords.Count)];
            if (current.Value > weight)
            {
                weight = current.Value;
                answer = current.Key;
            }
        }

        return answer;
    }

    public Dictionary<string, List<KeyValuePair<string, int>>> GetBigrams() => bigrams;

    public void SetBigrams(Dictionary<string, List<KeyValuePair<string, int>>> loadedBigrams) => bigrams = loadedBigrams;
}