using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace TheRavine.ViewControl
{
    public readonly struct ViewSector : IEquatable<ViewSector>
    {
        public const int Count = 16;
        public const float Degrees = 360f / Count;
        public const float HalfDegrees = Degrees * 0.5f;
        public const float HysteresisDegrees = 3f;

        private static readonly float2[] Forwards = new float2[Count];
        private static readonly float2[] Rights = new float2[Count];

        static ViewSector()
        {
            for (int i = 0; i < Count; i++)
            {
                math.sincos(math.radians(i * Degrees), out float s, out float c);
                s = math.abs(s) < 1e-6f ? 0f : s;
                c = math.abs(c) < 1e-6f ? 0f : c;
                Forwards[i] = new float2(s, c);
                Rights[i] = new float2(c, -s);
            }
        }

        public readonly byte Index;

        private ViewSector(int index) => Index = (byte)(index & (Count - 1));

        public static ViewSector FromIndex(int index) => new(index);

        public static ViewSector FromYaw(float yawDegrees) =>
            new((int)math.round(Wrap(yawDegrees) / Degrees));

        public static ViewSector FromYaw(float yawDegrees, ViewSector current)
        {
            float delta = DeltaAngle(current.Yaw, yawDegrees);
            return math.abs(delta) <= HalfDegrees + HysteresisDegrees ? current : FromYaw(yawDegrees);
        }

        public float2 Forward
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Forwards[Index];
        }

        public float2 Right
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Rights[Index];
        }

        public float Yaw => Index * Degrees;

        public ViewSector Rotated(int steps) => new(Index + steps);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ToWorld(float2 screen) => Rights[Index] * screen.x + Forwards[Index] * screen.y;

        public static float Wrap(float degrees)
        {
            degrees -= 360f * math.floor(degrees / 360f);
            return degrees >= 360f ? 0f : degrees;
        }

        public static float DeltaAngle(float from, float to)
        {
            float d = to - from;
            return d - 360f * math.floor((d + 180f) / 360f);
        }

        public bool Equals(ViewSector other) => Index == other.Index;
        public override bool Equals(object obj) => obj is ViewSector other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => $"Sector {Index} ({Yaw}°)";

        public static bool operator ==(ViewSector a, ViewSector b) => a.Index == b.Index;
        public static bool operator !=(ViewSector a, ViewSector b) => a.Index != b.Index;
    }
}
