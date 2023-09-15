using System;
public static class Extentions
{
    static public double JaroWinklerSimilarity(string str1, string str2)
    {
        if ((str1 == null) || (str2 == null))
        {
            return 0;
        }
        int matchingChars = 0;
        int transpositions = 0;
        // Вычисление максимальной разницы для определения границы сравнения
        int maxDistance = Math.Max(str1.Length, str2.Length) / 2 - 1;
        // Массивы для хранения информации о совпадающих символах
        bool[] str1Matches = new bool[str1.Length];
        bool[] str2Matches = new bool[str2.Length];
        // Поиск совпадающих символов
        for (int i = 0; i < str1.Length; i++)
        {
            int start = Math.Max(0, i - maxDistance);
            int end = Math.Min(i + maxDistance + 1, str2.Length);
            for (int j = start; j < end; j++)
            {
                if (!str2Matches[j] && str1[i] == str2[j])
                {
                    str1Matches[i] = true;
                    str2Matches[j] = true;
                    matchingChars++;
                    break;
                }
            }
        }
        // Если нет совпадающих символов, сходство равно 0
        if (matchingChars == 0)
        {
            return 0;
        }
        // Вычисление количества транспозиций
        int k = 0;
        for (int i = 0; i < str1.Length; i++)
        {
            if (str1Matches[i])
            {
                while (!str2Matches[k])
                {
                    k++;
                }
                if (str1[i] != str2[k])
                {
                    transpositions++;
                }
                k++;
            }
        }
        // Вычисление коэффициента сходства Джаро-Винкдера
        double jaroSimilarity = (double)matchingChars / (double)str1.Length;
        double winklerSimilarity = jaroSimilarity + ((transpositions * 0.1) * (1 - jaroSimilarity));
        return winklerSimilarity;
    }

}
