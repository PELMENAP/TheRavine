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

            if(!isCandidate)
                Tree = new KDTree(Points, Theta);
        }

        private static Point[] ProcessPoints(Point[] points)
        {
            TranslateToOrigin(ref points);
            Scale(ref points);
            return Resample(points, SAMPLING_RESOLUTION);
        }

        public float[] ComputeLocalShapeDescriptors(Point[] points)
        {

            int n = points.Length;
            float[] theta = new float[n];

            theta[0] = theta[n - 1] = 0;
            for (int i = 1; i < n - 1; i++)
                theta[i] = (float)(ShortAngle(points[i - 1], points[i], points[i + 1]) / Mathf.PI);

            return theta;
        }

        public static float ShortAngle(Point a, Point b, Point c)
        {
            float lengthAB = Geometry.SqrEuclideanDistance(a, b);
            float lengthBC = Geometry.SqrEuclideanDistance(b, c);
            if (lengthAB == 0 || lengthBC == 0)
                return 0;

            float dotProduct = ((b.X - a.X) * (c.X - b.X) + (b.Y - a.Y) * (c.Y - b.Y));
            float cosTheta = dotProduct / Mathf.Sqrt(lengthAB * lengthBC);

            return Mathf.Acos(Mathf.Clamp(cosTheta, -1f, 1f));
        }

        private static void Scale(ref Point[] points)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            foreach (var p in points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            float scale = Mathf.Max(maxX - minX, maxY - minY);
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Point((points[i].X - minX) / scale, (points[i].Y - minY) / scale, points[i].StrokeID);
            }
        }

        private static void TranslateToOrigin(ref Point[] points)
        {
            float cx = 0, cy = 0;
            foreach (var p in points)
            {
                cx += p.X;
                cy += p.Y;
            }
            cx /= points.Length;
            cy /= points.Length;

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Point(points[i].X - cx, points[i].Y - cy, points[i].StrokeID);
            }
        }

        public static Point[] Resample(Point[] points, int n)
        {
            Point[] newPoints = new Point[n];
            newPoints[0] = points[0];
            int numPoints = 1;

            float I = PathLength(points) / (n - 1);
            float D = 0;

            for (int i = 1; i < points.Length; i++)
            {
                if (points[i].StrokeID == points[i - 1].StrokeID)
                {
                    float d = (float)Mathf.Sqrt(Geometry.SqrEuclideanDistance(points[i - 1], points[i]));
                    if (D + d >= I)
                    {
                        Point firstPoint = points[i - 1];
                        while (D + d >= I && numPoints < n)
                        {
                            float t = (I - D) / d;
                            newPoints[numPoints++] = new Point(
                                (1 - t) * firstPoint.X + t * points[i].X,
                                (1 - t) * firstPoint.Y + t * points[i].Y,
                                points[i].StrokeID);

                            d -= I - D;
                            D = 0;
                            firstPoint = newPoints[numPoints - 1];
                        }
                        D = d;
                    }
                    else D += d;
                }
            }
            return newPoints;
        }

        private static float PathLength(Point[] points)
        {
            float length = 0;
            for (int i = 1; i < points.Length; i++)
                if (points[i].StrokeID == points[i - 1].StrokeID)
                    length += Geometry.SqrEuclideanDistance(points[i - 1], points[i]);
            return Mathf.Sqrt(length);
        }
    }
}