using UnityEngine;
using System.Collections.Generic;

namespace TheRavine.Extensions
{
    public static class DebugHelper
    {
        public static void DrawLinesFromPoints(List<Vector2Int> points, Color color, float duration = 5f)
        {
            if (points == null || points.Count < 2) return;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 start = new(points[i].x, points[i].y, 0);
                Vector3 end = new(points[i + 1].x, points[i + 1].y, 0);
                Debug.DrawLine(start, end, color, duration);
            }
        }

        public static void DrawLinesFromPointLists(List<List<Vector2Int>> pointLists, Color color, float duration = 5f)
        {
            if (pointLists == null) return;

            foreach (var points in pointLists)
            {
                DrawLinesFromPoints(points, color, duration);
            }
        }
        public static void DrawRay(Vector3Int point, Color color, float duration = 5f, float size = 0.2f)
        {
            Vector3 pos = new(point.x, point.y, point.z);
            
            Debug.DrawLine(pos + new Vector3(-size, -size, 0), pos + new Vector3(size, size, 0), color, duration);
            Debug.DrawLine(pos + new Vector3(-size, size, 0), pos + new Vector3(size, -size, 0), color, duration);
            Debug.DrawLine(Vector3.zero, pos, color, duration);
        }
        public static void DrawPoints(List<Vector2Int> points, Color color, float duration = 5f, float size = 0.2f)
        {
            if (points == null) return;

            foreach (var point in points)
            {
                Vector3 pos = new(point.x, point.y, 0);
                Debug.DrawLine(pos + new Vector3(-size, -size, 0), pos + new Vector3(size, size, 0), color, duration);
                Debug.DrawLine(pos + new Vector3(-size, size, 0), pos + new Vector3(size, -size, 0), color, duration);
            }
        }
        public static void DrawSegment(Vector2Int point1, Vector2Int point2, Color color, float duration = 5f)
        {
            if (point1 == null) return;

            Debug.DrawLine(new Vector3(point1.x, point1.y, 0), new Vector3(point2.x, point2.y, 0), color, duration);
        }
        public static void DrawPointsFromLists(List<List<Vector2Int>> pointLists, Color color, float size = 0.3f, float duration = 5f)
        {
            if (pointLists == null) return;

            foreach (var points in pointLists)
            {
                DrawPoints(points, color, size, duration);
            }
        }
    }
}