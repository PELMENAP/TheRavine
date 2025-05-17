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

            return new Result(string.IsNullOrEmpty(gestureClass) ? "No match" : gestureClass, Mathf.Max((minDistance - 2.0f) / -2.0f, 0.0f));
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

    public readonly struct Result {

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
        private KDTreeNode root;
        private const int Dimensions = 3;
        private readonly float spatialWeight = 0.7f;
        private readonly float angleWeight = 0.3f;
        private struct PointData
        {
            public Point Point;
            public float Theta;
            public int Index;
        }

        private class KDTreeNode
        {
            public Point Point;
            public float Theta;
            public int Index;
            public int Axis;
            public KDTreeNode Left, Right;
        }

        public KDTree(Point[] points, float[] theta)
        {
            var data = new PointData[points.Length];
            for (int i = 0; i < points.Length; i++)
                data[i] = new PointData { Point = points[i], Theta = theta[i], Index = i };

            root = BuildTree(data, 0, points.Length, 0);
        }

        private KDTreeNode BuildTree(PointData[] data, int start, int end, int depth)
        {
            if (start >= end) return null;

            int axis = depth % Dimensions;
            int mid = (start + end) / 2;
            QuickSelect(data, start, end - 1, mid, axis);

            var median = data[mid];
            var node = new KDTreeNode
            {
                Point = median.Point,
                Theta = median.Theta,
                Index = median.Index,
                Axis = axis,
                Left = BuildTree(data, start, mid, depth + 1),
                Right = BuildTree(data, mid + 1, end, depth + 1)
            };
            return node;
        }

        private void QuickSelect(PointData[] data, int left, int right, int k, int axis)
        {
            while (true)
            {
                if (left == right) return;
                int pivotIndex = Partition(data, left, right, axis, (left + right) / 2);
                if (k == pivotIndex) return;
                else if (k < pivotIndex) right = pivotIndex - 1;
                else left = pivotIndex + 1;
            }
        }

        private int Partition(PointData[] data, int left, int right, int axis, int pivotIndex)
        {
            var pivotValue = data[pivotIndex];
            Swap(data, pivotIndex, right);
            int storeIndex = left;

            for (int i = left; i < right; i++)
            {
                if (Compare(data[i], pivotValue, axis) < 0)
                {
                    Swap(data, storeIndex, i);
                    storeIndex++;
                }
            }
            Swap(data, right, storeIndex);
            return storeIndex;
        }

        private void Swap(PointData[] data, int a, int b)
        {
            PointData tmp = data[a];
            data[a] = data[b];
            data[b] = tmp;
        }

        private int Compare(PointData a, PointData b, int axis)
        {
            if (axis == 0) return a.Point.X.CompareTo(b.Point.X);
            if (axis == 1) return a.Point.Y.CompareTo(b.Point.Y);
            return a.Theta.CompareTo(b.Theta);
        }

        public NearestNeighbor FindNearest(Point queryPoint, float queryTheta)
        {
            KDTreeNode best = null;
            float bestDist = float.MaxValue;
            Search(root, queryPoint, queryTheta, ref best, ref bestDist);
            return new NearestNeighbor(best.Point, best.Theta, best.Index, bestDist);
        }

        private void Search(KDTreeNode node, Point q, float qTheta, ref KDTreeNode best, ref float bestDist)
        {
            if (node == null) return;

            float dist = Distance(q, qTheta, node.Point, node.Theta);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = node;
            }

            bool goLeft = node.Axis switch
            {
                0 => q.X < node.Point.X,
                1 => q.Y < node.Point.Y,
                _ => qTheta < node.Theta
            };

            var first = goLeft ? node.Left : node.Right;
            var second = goLeft ? node.Right : node.Left;

            Search(first, q, qTheta, ref best, ref bestDist);

            float axisDiff = node.Axis switch
            {
                0 => (q.X - node.Point.X),
                1 => (q.Y - node.Point.Y),
                _ => (qTheta - node.Theta)
            };
            float axisDist = axisDiff * axisDiff;

            if (axisDist * spatialWeight < bestDist)
                Search(second, q, qTheta, ref best, ref bestDist);
        }

        private float Distance(Point a, float thetaA, Point b, float thetaB)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float spatial = dx * dx + dy * dy;
            float angle = (thetaA - thetaB) * (thetaA - thetaB);
            return spatialWeight * spatial + angleWeight * angle;
        }

        public class NearestNeighbor
        {
            public Point Point;
            public float Theta;
            public int Index;
            public float Distance;

            public NearestNeighbor(Point p, float t, int i, float d)
            {
                Point = p;
                Theta = t;
                Index = i;
                Distance = d;
            }
        }
    }
}