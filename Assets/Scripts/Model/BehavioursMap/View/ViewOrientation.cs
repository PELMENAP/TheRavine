using System;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.ViewControl
{
    public static class ViewOrientation
    {
        public const int BakedSides = 8;
        public const float BakedSideDegrees = 360f / BakedSides;

        private static readonly int ViewBasisId = Shader.PropertyToID("_ViewBasis");
        private static readonly int ViewColumnId = Shader.PropertyToID("_ViewColumn");

        private static IViewOriented[] items = new IViewOriented[128];
        private static int count;
        private static float yaw;
        private static float pitch;
        private static float2 forward = new(0f, 1f);
        private static float2 right = new(1f, 0f);
        private static Quaternion rotation = Quaternion.identity;
        private static Quaternion yawRotation = Quaternion.identity;
        private static ViewSector sector;
        private static int column;
        private static Vector3 pivot;
        private static bool hasPivot;

        public static float YawDegrees => yaw;
        public static float PitchDegrees => pitch;
        public static float2 Forward => forward;
        public static float2 Right => right;
        public static Quaternion Rotation => rotation;
        public static Quaternion YawRotation => yawRotation;
        public static ViewSector Sector => sector;
        public static int Column => column;

        public static bool HasPivot => hasPivot;
        public static Vector3 Pivot => pivot;

        public static event Action ViewChanged;

        public static event Action<ViewSector> SectorChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Array.Clear(items, 0, count);
            count = 0;
            yaw = 0f;
            pitch = 0f;
            forward = new float2(0f, 1f);
            right = new float2(1f, 0f);
            rotation = Quaternion.identity;
            yawRotation = Quaternion.identity;
            sector = default;
            column = 0;
            pivot = Vector3.zero;
            hasPivot = false;
            ViewChanged = null;
            SectorChanged = null;
            PushShaderGlobals();
        }

        public static float2 ToWorld(float2 screen) => right * screen.x + forward * screen.y;

        public static void SetPivot(Vector3 value)
        {
            pivot = value;
            hasPivot = true;
        }

        public static void ClearPivot() => hasPivot = false;

        public static void Set(float newYaw, float newPitch)
        {
            newYaw = ViewSector.Wrap(newYaw);
            if (newYaw == yaw && newPitch == pitch) return;

            bool yawChanged = newYaw != yaw;
            yaw = newYaw;
            pitch = newPitch;

            if (yawChanged)
            {
                math.sincos(math.radians(newYaw), out float s, out float c);
                forward = new float2(s, c);
                right = new float2(c, -s);
                yawRotation = Quaternion.Euler(0f, newYaw, 0f);
            }
            rotation = Quaternion.Euler(newPitch, newYaw, 0f);

            ViewSector newSector = ViewSector.FromYaw(newYaw, sector);
            bool sectorChanged = newSector != sector;
            sector = newSector;
            column = ColumnOf(newYaw);

            PushShaderGlobals();

            for (int i = count - 1; i >= 0; i--)
                items[i].OnViewChanged(rotation);

            ViewChanged?.Invoke();
            if (sectorChanged) SectorChanged?.Invoke(sector);
        }

        public static int ColumnOf(float yawDegrees) =>
            (int)math.round(ViewSector.Wrap(yawDegrees) / BakedSideDegrees) % BakedSides;

        public static void Register(IViewOriented item)
        {
            if (item == null) return;

            int slot = item.ViewSlot;
            if ((uint)slot < (uint)count && ReferenceEquals(items[slot], item)) return;

            if (count == items.Length)
                Array.Resize(ref items, count * 2);

            item.ViewSlot = count;
            items[count++] = item;
            item.OnViewChanged(rotation);
        }

        public static void Unregister(IViewOriented item)
        {
            if (item == null) return;

            int slot = item.ViewSlot;
            if ((uint)slot >= (uint)count || !ReferenceEquals(items[slot], item)) return;

            int last = count - 1;
            if (slot != last)
            {
                IViewOriented moved = items[last];
                items[slot] = moved;
                moved.ViewSlot = slot;
            }

            items[last] = null;
            count = last;
            item.ViewSlot = -1;
        }

        private static void PushShaderGlobals()
        {
            Shader.SetGlobalVector(ViewBasisId, new Vector4(forward.x, forward.y, right.x, right.y));
            Shader.SetGlobalFloat(ViewColumnId, column);
        }
    }
}
