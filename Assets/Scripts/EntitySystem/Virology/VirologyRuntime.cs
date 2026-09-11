using Unity.Collections;
using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public static class VirologyRuntime
    {
        public const uint PrototypeSeed = 0x5EEDC0DEu;

        private static NativeArray<ProteinDescriptor> _descriptors;
        private static NativeArray<float> _cellBias;
        private static NativeArray<float> _prototype;
        private static bool _ready;

        public static NativeArray<ProteinDescriptor> Descriptors => _descriptors;
        public static NativeArray<float> CellBias => _cellBias;
        public static NativeArray<float> Prototype => _prototype;
        public static bool IsReady => _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_ready) return;

            _descriptors = new NativeArray<ProteinDescriptor>(ProteinTable.ActionCount,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _cellBias = new NativeArray<float>(ProteinTable.ActionCount,
                Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < ProteinTable.ActionCount; i++)
            {
                _descriptors[i] = ProteinTable.Descriptors[i];
                _cellBias[i] = ProteinTable.CellBias[i];
            }

            _prototype = TranslationTable.CreatePrototype(PrototypeSeed, Allocator.Persistent);
            _ready = true;
            Application.quitting += Shutdown;
        }

        private static void Shutdown()
        {
            Application.quitting -= Shutdown;
            if (!_ready) return;
            _ready = false;
            if (_descriptors.IsCreated) _descriptors.Dispose();
            if (_cellBias.IsCreated) _cellBias.Dispose();
            if (_prototype.IsCreated) _prototype.Dispose();
        }
    }
}