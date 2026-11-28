using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.Generator
{
    public static class CloudNoise
    {
        public const int Cells = 16;

        public static float Lattice(int x, int y)
            => (math.hash(new int2(x & (Cells - 1), y & (Cells - 1))) & 0xFFFF) * (1f / 65535f);

        public static float Value(int x, int y, int size)
        {
            float step = Cells / (float)size;
            float gx = (x + 0.5f) * step;
            float gy = (y + 0.5f) * step;
            int ix = (int)math.floor(gx);
            int iy = (int)math.floor(gy);
            float fx = gx - ix;
            float fy = gy - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = math.lerp(Lattice(ix, iy), Lattice(ix + 1, iy), fx);
            float b = math.lerp(Lattice(ix, iy + 1), Lattice(ix + 1, iy + 1), fx);
            return math.lerp(a, b, fy);
        }

        public static Texture2D Create(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.R8, true, true)
            {
                name = "CloudNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new NativeArray<byte>(size * size, Allocator.Temp);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[x + y * size] = (byte)(Value(x, y, size) * 255f + 0.5f);

            texture.SetPixelData(pixels, 0);
            texture.Apply(true, false);
            pixels.Dispose();
            return texture;
        }
    }
}
