using System;
using UnityEngine;

namespace TheRavine.Extentions
{
    public static class Extention
    {
        public static double JaroWinklerSimilarity(string str1, string str2)
        {
            if ((str1 == null) || (str2 == null))
                return 0;
            int matchingChars = 0;
            int transpositions = 0;
            int maxDistance = Math.Max(str1.Length, str2.Length) / 2 - 1;
            bool[] str1Matches = new bool[str1.Length];
            bool[] str2Matches = new bool[str2.Length];
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
            if (matchingChars == 0)
                return 0;
            int k = 0;
            for (int i = 0; i < str1.Length; i++)
            {
                if (str1Matches[i])
                {
                    while (!str2Matches[k])
                        k++;
                    if (str1[i] != str2[k])
                        transpositions++;
                    k++;
                }
            }
            double jaroSimilarity = (double)matchingChars / (double)str1.Length;
            double winklerSimilarity = jaroSimilarity + ((transpositions * 0.1) * (1 - jaroSimilarity));
            return winklerSimilarity;
        }
        private static Vector2 RoundVector2(Vector2 vec) => new Vector2(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));
        public static Vector2 RoundVector2D(Vector3 vec) => RoundVector2((Vector2)vec);
        public static Vector2 GetRandomPointAround(Vector2 centerPoint, float factor)
        {
            double[] randomPoint = DistributionCache.GetCachedDistribution().Generate();
            return new Vector2((float)(centerPoint.x + randomPoint[0] * factor), (float)(centerPoint.y + randomPoint[1] * factor));
        }
        public static Vector2 CalculateQuadraticBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            return uu * p0 + 2 * u * t * p1 + tt * p2;
        }
        public static bool CheckDistance(Vector2 from, Vector2 to, float threshold) => Vector2.Distance(from, to) < threshold;
        public static Vector2 RotateVector(Vector2 vector, float angle)
        {
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }
        public static float GetRandomValue(float min, float max) => RavineRandom.RangeFloat(min, max);
        public static Vector2 PerpendicularClockwise(Vector2 vector) => new Vector2(vector.y, -vector.x);
        public static Vector2 PerpendicularCounterClockwise(Vector2 vector) => new Vector2(-vector.y, vector.x);
        public static void GenerateBezierPoints(Vector2 start, Vector2 control, Vector2 end, byte bezierDetail, ref Vector2[] bezierPoints)
        {
            for (byte i = 0; i <= bezierDetail; i++)
                bezierPoints[i] = CalculateQuadraticBezierPoint(i / (float)bezierDetail, start, control, end);
        }
        public static bool ComparePercent(int value) => RavineRandom.Hundred() <= value;
    }

    public class Vector2Comparer : System.Collections.Generic.IComparer<Vector2>
    {
        public int Compare(Vector2 v1, Vector2 v2)
        {
            if (v1.x.CompareTo(v2.x) != 0)
                return v1.x.CompareTo(v2.x);
            return v1.y.CompareTo(v2.y);
        }
    }

    public struct Pair<T, U>
    {
        public Pair(T first, U second)
        {
            this.First = first;
            this.Second = second;
        }
        public T First { get; set; }
        public U Second { get; set; }
    };
}