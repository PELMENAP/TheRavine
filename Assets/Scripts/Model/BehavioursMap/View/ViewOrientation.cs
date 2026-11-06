using System;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.ViewControl
{
    public static class ViewOrientation
    {
        private static readonly int ViewBasisId = Shader.PropertyToID("_ViewBasis");
        private static readonly int ViewColumnId = Shader.PropertyToID("_ViewColumn");

        private static IViewOriented[] items = new IViewOriented[128];
        private static int count;
        private static ViewFacing facing;
        private static float pitch;
        private static Quaternion rotation = Quaternion.identity;
        private static Quaternion yawRotation = Quaternion.identity;

        public static ViewFacing Facing => facing;
        public static float PitchDegrees => pitch;
        public static Quaternion Rotation => rotation;
        public static Quaternion YawRotation => yawRotation;

        public static event Action<ViewFacing> FacingChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Array.Clear(items, 0, count);
            count = 0;
            facing = ViewFacing.PlusZ;
            pitch = 0f;
            rotation = Quaternion.identity;
            yawRotation = Quaternion.identity;
            FacingChanged = null;
            PushShaderGlobals();
        }

        public static void Set(ViewFacing newFacing, float newPitch)
        {
            bool facingChanged = newFacing != facing;
            if (!facingChanged && newPitch == pitch) return;

            facing = newFacing;
            pitch = newPitch;
            float yaw = newFacing.Yaw();
            rotation = Quaternion.Euler(newPitch, yaw, 0f);
            yawRotation = Quaternion.Euler(0f, yaw, 0f);

            if (facingChanged) PushShaderGlobals();

            for (int i = count - 1; i >= 0; i--)
                items[i].OnViewChanged(facing, rotation);

            if (facingChanged) FacingChanged?.Invoke(facing);
        }

        public static void Register(IViewOriented item)
        {
            if (item == null) return;

            int slot = item.ViewSlot;
            if ((uint)slot < (uint)count && ReferenceEquals(items[slot], item)) return;

            if (count == items.Length)
                Array.Resize(ref items, count * 2);

            item.ViewSlot = count;
            items[count++] = item;
            item.OnViewChanged(facing, rotation);
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
            int2 f = facing.Forward();
            int2 r = facing.Right();
            Shader.SetGlobalVector(ViewBasisId, new Vector4(f.x, f.y, r.x, r.y));
            Shader.SetGlobalFloat(ViewColumnId, (int)facing);
        }
    }
}
