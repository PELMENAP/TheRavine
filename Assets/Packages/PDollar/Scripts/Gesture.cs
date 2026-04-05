using UnityEngine;

namespace TheRavine.Extensions
{
    public class Gesture
    {
        public Point[] Points;
        public float[] Theta;
        public string Name;

        public KDTree Tree { get; private set; }

        public const int SAMPLING_RESOLUTION = 32;

        public Gesture(Point[] points, string gestureName = "", bool isCandidate = false)
        {
            Name = gestureName;
            Points = ProcessPoints(points);
            Theta = ComputeLocalShapeDescriptors(Points);

            if (!isCandidate)
                Tree = new KDTree(Points, Theta);
        }

        private static Point[] ProcessPoints(Point[] points)
        {
            TranslateToOrigin(ref points);
            Scale(ref points);
            return Resample(points, SAMPLING_RESOLUTION);
        }

        public static float[] ComputeLocalShapeDescriptors(Point[] points)
        {
            int n = points.Length;
            float[] theta = new float[n];
            for (int i = 1; i < n - 1; i++)
                theta[i] = ShortAngle(points[i - 1], points[i], points[i + 1]) / Mathf.PI;
            return theta;
        }

        public static float ShortAngle(Point a, Point b, Point c)
        {
            float sqrAB = Geometry.SqrEuclideanDistance(a, b);
            float sqrBC = Geometry.SqrEuclideanDistance(b, c);
            if (sqrAB == 0f || sqrBC == 0f) return 0f;

            float dot = (b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y);
            return Mathf.Acos(Mathf.Clamp(dot / Mathf.Sqrt(sqrAB * sqrBC), -1f, 1f));
        }

        private static void Scale(ref Point[] points)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var p in points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            float scale = Mathf.Max(maxX - minX, maxY - minY);
            if (scale == 0f) return;

            float invScale = 1f / scale;
            for (int i = 0; i < points.Length; i++)
                points[i] = new Point(
                    (points[i].X - minX) * invScale,
                    (points[i].Y - minY) * invScale,
                    points[i].StrokeID);
        }

        private static void TranslateToOrigin(ref Point[] points)
        {
            int n = points.Length;
            float cx = 0f, cy = 0f;
            foreach (var p in points) { cx += p.X; cy += p.Y; }

            float invN = 1f / n;
            cx *= invN;
            cy *= invN;

            for (int i = 0; i < n; i++)
                points[i] = new Point(points[i].X - cx, points[i].Y - cy, points[i].StrokeID);
        }

        public static Point[] Resample(Point[] points, int n)
        {
            Point[] newPoints = new Point[n];
            newPoints[0] = points[0];
            int numPoints = 1;

            float I = PathLength(points) / (n - 1);
            float D = 0f;

            for (int i = 1; i < points.Length && numPoints < n; i++)
            {
                if (points[i].StrokeID != points[i - 1].StrokeID) continue;

                float d = Mathf.Sqrt(Geometry.SqrEuclideanDistance(points[i - 1], points[i]));
                if (D + d >= I)
                {
                    Point prev = points[i - 1];
                    while (D + d >= I && numPoints < n)
                    {
                        float t = (I - D) / d;
                        Point next = new Point(
                            prev.X + t * (points[i].X - prev.X),
                            prev.Y + t * (points[i].Y - prev.Y),
                            points[i].StrokeID);
                        newPoints[numPoints++] = next;
                        d -= I - D;
                        D = 0f;
                        prev = next;
                    }
                    D = d;
                }
                else D += d;
            }

            Point last = points[points.Length - 1];
            while (numPoints < n)
                newPoints[numPoints++] = last;

            return newPoints;
        }

        private static float PathLength(Point[] points)
        {
            float length = 0f;
            for (int i = 1; i < points.Length; i++)
                if (points[i].StrokeID == points[i - 1].StrokeID)
                    length += Mathf.Sqrt(Geometry.SqrEuclideanDistance(points[i - 1], points[i]));
            return length;
        }
    }
}