using System;
using UnityEngine;

namespace TheRavine.Extensions
{
    public static class PointCloudRecognizerPlus
    {
        public static Result Classify(Gesture candidate, Gesture[] trainingSet)
        {
            if (trainingSet == null || trainingSet.Length == 0)
                return new Result("No match", 0.0f);

            float minDistance = float.MaxValue;
            string gestureClass = "";
            foreach (Gesture template in trainingSet)
            {
                float dist = CloudDistance(candidate.Points, candidate.Theta, template, 0);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    gestureClass = template.Name;
                }
            }

            return new Result(
                string.IsNullOrEmpty(gestureClass) ? "No match" : gestureClass,
                Mathf.Max((minDistance - 2.0f) / -2.0f, 0.0f));
        }

        private static float CloudDistance(Point[] points1, float[] theta1, Gesture gesture, int startIndex)
        {
            int n = points1.Length;
            float sum = 0f;
            int i = startIndex;

            do
            {
                var nearest = gesture.Tree.FindNearest(points1[i], theta1[i]);
                float weight = 1f - ((i - startIndex + n) % n) / (float)n;
                sum += weight * nearest.Distance;
                i = (i + 1) % n;
            } while (i != startIndex);

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
    public class KDTree
    {
        private const int Dims = 3;
        private const float SpatialW = 0.7f;
        private const float AngleW = 0.3f;

        private struct Node
        {
            public float X, Y, Theta;
            public int Left, Right;
            public byte Axis;
        }

        private struct PointData
        {
            public float X, Y, Theta;
        }

        private readonly Node[] _nodes;
        private int _count;

        public readonly struct NearestNeighbor
        {
            public readonly float Distance;
            public NearestNeighbor(float distance) => Distance = distance;
        }

        public KDTree(Point[] points, float[] theta)
        {
            int n = points.Length;
            _nodes = new Node[n];

            var data = new PointData[n];
            for (int i = 0; i < n; i++)
                data[i] = new PointData { X = points[i].X, Y = points[i].Y, Theta = theta[i] };

            if (n > 0) BuildTree(data, 0, n, 0);
        }

        private int BuildTree(PointData[] data, int start, int end, int depth)
        {
            if (start >= end) return -1;

            int axis = depth % Dims;
            int mid = (start + end) >> 1;
            QuickSelect(data, start, end - 1, mid, axis);

            int idx = _count++;
            ref Node node = ref _nodes[idx];
            node.X = data[mid].X;
            node.Y = data[mid].Y;
            node.Theta = data[mid].Theta;
            node.Axis = (byte)axis;
            node.Left = BuildTree(data, start, mid, depth + 1);
            node.Right = BuildTree(data, mid + 1, end, depth + 1);
            return idx;
        }

        private static void QuickSelect(PointData[] data, int left, int right, int k, int axis)
        {
            while (left < right)
            {
                int pivotIdx = Partition(data, left, right, axis, (left + right) >> 1);
                if (k == pivotIdx) return;
                if (k < pivotIdx) right = pivotIdx - 1;
                else left = pivotIdx + 1;
            }
        }

        private static int Partition(PointData[] data, int left, int right, int axis, int pivotIdx)
        {
            float pivot = GetAxis(data[pivotIdx], axis);
            Swap(ref data[pivotIdx], ref data[right]);
            int store = left;
            for (int i = left; i < right; i++)
            {
                if (GetAxis(data[i], axis) < pivot)
                    Swap(ref data[store++], ref data[i]);
            }
            Swap(ref data[right], ref data[store]);
            return store;
        }

        private static void Swap(ref PointData a, ref PointData b)
        {
            PointData tmp = a; a = b; b = tmp;
        }

        private static float GetAxis(in PointData p, int axis) => axis switch
        {
            0 => p.X,
            1 => p.Y,
            _ => p.Theta
        };

        public NearestNeighbor FindNearest(Point query, float queryTheta)
        {
            if (_nodes.Length == 0) return new NearestNeighbor(float.MaxValue);

            float bestDist = float.MaxValue;
            int bestIdx = 0;
            Search(0, query.X, query.Y, queryTheta, ref bestDist, ref bestIdx);
            return new NearestNeighbor(bestDist);
        }

        private void Search(int nodeIdx, float qx, float qy, float qt, ref float bestDist, ref int bestIdx)
        {
            if (nodeIdx < 0) return;

            ref readonly Node node = ref _nodes[nodeIdx];

            float dx = qx - node.X;
            float dy = qy - node.Y;
            float dt = qt - node.Theta;
            float dist = SpatialW * (dx * dx + dy * dy) + AngleW * (dt * dt);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = nodeIdx;
            }

            float axisDiff = node.Axis switch { 0 => dx, 1 => dy, _ => dt };
            float axisWeight = node.Axis == 2 ? AngleW : SpatialW;
            bool goLeft = axisDiff < 0f;

            Search(goLeft ? node.Left : node.Right, qx, qy, qt, ref bestDist, ref bestIdx);

            if (axisDiff * axisDiff * axisWeight < bestDist)
                Search(goLeft ? node.Right : node.Left, qx, qy, qt, ref bestDist, ref bestIdx);
        }
    }
}