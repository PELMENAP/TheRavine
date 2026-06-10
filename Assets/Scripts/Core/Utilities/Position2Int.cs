using System.Runtime.CompilerServices;
using UnityEngine;

namespace TheRavine.Extensions
{
    public static class Position2Int
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Pack(Vector2Int pos) => ((long)pos.x << 32) | (uint)pos.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Pack(int x, int y) => ((long)x << 32) | (uint)y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int UnpackToVector(long key) => 
            new((int)(key >> 32), (int)key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unpack(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)key;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetX(long key) => (int)(key >> 32);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetY(long key) => (int)key;
    }
}