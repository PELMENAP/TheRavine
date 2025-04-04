using System;
using System.Collections.Generic;

using UnityEngine;

namespace TheRavine.Extensions
{
    public static class PointCloudRecognizerPlus
    {
        public static Result Classify(Gesture candidate, Gesture[] trainingSet)
        {
            if (trainingSet == null || trainingSet.Length == 0)
                return new Result() { GestureClass = "No match", Score = 0.0f };

            float minDistance = float.MaxValue;
            string gestureClass = "";
            foreach (Gesture template in trainingSet)
            {
                float dist = CloudDistance(candidate.Points, candidate.Theta, template.Points, template.Theta);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    gestureClass = template.Name;
                }
            }

			return gestureClass == "" ? new Result() {GestureClass = "No match", Score = 0.0f} : new Result() {GestureClass = gestureClass, Score = Mathf.Max((minDistance - 2.0f) / -2.0f, 0.0f)};
        }
        private static float CloudDistance(Point[] points1, float[] theta1, Point[] points2, float[] theta2)
        {
            bool[] matched = new bool[points2.Length];
            Array.Clear(matched, 0, points2.Length);

            float sum = 0; // computes the cost of the cloud alignment
            int index;

            // match points1 to points2
            for (int i = 0; i < points1.Length; i++)
            {
                sum += GetClosestPointFromCloud(points1[i], theta1[i], points2, theta2, out index);
                matched[index] = true;
            }

            // match points2 to points1
            for (int i = 0; i < points2.Length; i++)
                if (!matched[i])
                    sum += GetClosestPointFromCloud(points2[i], theta2[i], points1, theta1, out index);

            return sum;
        }
        private static float GetClosestPointFromCloud(Point p, float theta, Point[] cloud, float[] thetaCloud, out int indexMin)
        {
            float min = float.MaxValue;
            indexMin = -1;
            for (int i = 0; i < cloud.Length; i++)
            {
                if (p.StrokeID != cloud[i].StrokeID) 
                    continue;
                
                float sqrDist = Geometry.SqrEuclideanDistance(p, cloud[i]) + (theta - thetaCloud[i]) * (theta - thetaCloud[i]);
                if (sqrDist < min)
                {
                    min = sqrDist;
                    indexMin = i;
                }
            }
            return (float)Math.Sqrt(min);
        }
    }

    public class Point
    {
        public float X, Y;
        public int StrokeID;      

        public Point(float x, float y, int strokeId)
        {
            this.X = x;
            this.Y = y;
            this.StrokeID = strokeId;
        }
    }

    public struct Result {

		public string GestureClass;
		public float Score;
	}

    public static class Geometry
    {
        public static float SqrEuclideanDistance(Point a, Point b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        }
        public static float EuclideanDistance(Point a, Point b)
        {
            return (float)Math.Sqrt(SqrEuclideanDistance(a, b));
        }
    }
}