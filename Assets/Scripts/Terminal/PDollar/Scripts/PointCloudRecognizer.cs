using UnityEngine;

namespace TheRavine.Extensions
{
    public static class PointCloudRecognizerPlus
    {
        private const float SpatialWeight = 0.7f;
        private const float AngleWeight = 0.3f;

        public static Result Classify(Gesture candidate, Gesture[] trainingSet)
        {
            if (trainingSet == null || trainingSet.Length == 0)
                return new Result("No match", 0.0f);

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

            return new Result(
                string.IsNullOrEmpty(gestureClass) ? "No match" : gestureClass,
                Mathf.Max((minDistance - 2.0f) / -2.0f, 0.0f)
            );
        }

        private static float CloudDistance(Point[] points1, float[] theta1, Point[] points2, float[] theta2)
        {
            int n = points1.Length;
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                float minDist = float.MaxValue;
                float qx = points1[i].X;
                float qy = points1[i].Y;
                float qt = theta1[i];

                for (int j = 0; j < n; j++)
                {
                    float dx = qx - points2[j].X;
                    float dy = qy - points2[j].Y;
                    float dt = qt - theta2[j];
                    float dist = SpatialWeight * (dx * dx + dy * dy) + AngleWeight * (dt * dt);
                    if (dist < minDist) minDist = dist;
                }

                float weight = 1f - (float)i / n;
                sum += weight * minDist;
            }

            return sum;
        }
    }

    public readonly struct Point
    {
        public readonly float X, Y;
        public readonly int StrokeID;

        public Point(float x, float y, int strokeId)
        {
            X = x;
            Y = y;
            StrokeID = strokeId;
        }
    }

    public readonly struct Result
    {
        public readonly string GestureClass;
        public readonly float Score;

        public Result(string gestureClass, float score)
        {
            GestureClass = gestureClass;
            Score = score;
        }
    }

    public static class Geometry
    {
        public static float SqrEuclideanDistance(Point a, Point b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}