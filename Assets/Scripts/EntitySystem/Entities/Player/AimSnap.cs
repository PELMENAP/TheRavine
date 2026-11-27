using System.Runtime.CompilerServices;
using Unity.Mathematics;

using TheRavine.Extensions;
using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public static class AimSnap
    {
        public const long NoCell = long.MinValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Resolve(float2 target, long previousCell, float hysteresis)
        {
            float2 grid = target * MapGenerator.InvScale;

            if (previousCell != NoCell)
            {
                Position2Int.Unpack(previousCell, out int px, out int pz);
                float2 prevCenter = new float2((px >> MapGenerator.CellShift) + 0.5f, (pz >> MapGenerator.CellShift) + 0.5f);
                float2 delta = math.abs(grid - prevCenter);
                if (delta.x < 0.5f + hysteresis && delta.y < 0.5f + hysteresis)
                    return previousCell;
            }

            int2 cell = (int2)math.floor(grid);
            return Position2Int.Pack(cell.x << MapGenerator.CellShift, cell.y << MapGenerator.CellShift);
        }
    }
}
