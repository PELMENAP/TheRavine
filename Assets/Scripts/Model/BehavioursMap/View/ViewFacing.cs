using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TheRavine.ViewControl
{
    public enum ViewFacing : byte
    {
        PlusZ = 0,
        MinusZ = 1,
        PlusX = 2,
        MinusX = 3
    }

    public static class ViewFacingExt
    {
        private static readonly int2[] Forwards = { new int2(0, 1), new int2(0, -1), new int2(1, 0), new int2(-1, 0) };
        private static readonly int2[] Rights = { new int2(1, 0), new int2(-1, 0), new int2(0, -1), new int2(0, 1) };
        private static readonly float[] Yaws = { 0f, 180f, 90f, 270f };
        private static readonly ViewFacing[] Cycle = { ViewFacing.PlusZ, ViewFacing.PlusX, ViewFacing.MinusZ, ViewFacing.MinusX };
        private static readonly int[] CycleIndex = { 0, 2, 1, 3 };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 Forward(this ViewFacing facing) => Forwards[(int)facing];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 Right(this ViewFacing facing) => Rights[(int)facing];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Yaw(this ViewFacing facing) => Yaws[(int)facing];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ViewFacing Rotate(this ViewFacing facing, int quarterTurns) =>
            Cycle[(CycleIndex[(int)facing] + quarterTurns) & 3];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ViewFacing Opposite(this ViewFacing facing) => facing.Rotate(2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ToWorld(this ViewFacing facing, float2 screen)
        {
            int2 r = Rights[(int)facing];
            int2 f = Forwards[(int)facing];
            return new float2(r.x, r.y) * screen.x + new float2(f.x, f.y) * screen.y;
        }
    }
}
