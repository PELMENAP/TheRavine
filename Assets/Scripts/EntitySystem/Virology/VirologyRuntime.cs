using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public static class VirologyRuntime
    {
        public const uint PrototypeSeed = 0x5EEDC0DEu;

        private static ProteinDescriptor[] _descriptors;
        private static float[] _cellBias;
        private static float[] _prototype;
        private static bool _ready;

        public static ProteinDescriptor[] Descriptors => _descriptors;
        public static float[] CellBias => _cellBias;
        public static float[] Prototype => _prototype;
        public static bool IsReady => _ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_ready) return;

            _descriptors = new ProteinDescriptor[ProteinTable.ActionCount];
            _cellBias = new float[ProteinTable.ActionCount];

            for (int i = 0; i < ProteinTable.ActionCount; i++)
            {
                _descriptors[i] = ProteinTable.Descriptors[i];
                _cellBias[i] = ProteinTable.CellBias[i];
            }

            _prototype = TranslationTable.CreatePrototype(PrototypeSeed);
            _ready = true;
            Application.quitting += Shutdown;
        }

        private static void Shutdown()
        {
            Application.quitting -= Shutdown;
            if (!_ready) return;
            _ready = false;
        }
    }
}