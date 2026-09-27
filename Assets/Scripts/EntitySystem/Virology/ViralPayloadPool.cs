using System;

namespace TheRavine.EntityControl.Virology
{
    public static class ViralPayloadPool
    {
        private static ushort[][] _free = new ushort[16][];
        private static int _count;

        public static int Pooled => _count;

        public static ushort[] Rent()
        {
            if (_count == 0) return new ushort[InfectionService.PayloadCapacity];
            var buffer = _free[--_count];
            _free[_count] = null;
            return buffer;
        }

        public static void Return(ushort[] buffer)
        {
            if (buffer == null || buffer.Length != InfectionService.PayloadCapacity) return;
            if (_count == _free.Length) Array.Resize(ref _free, _free.Length << 1);
            _free[_count++] = buffer;
        }

        public static void Release(ref ViralPayload payload)
        {
            Return(payload.Codons);
            payload = default;
        }
    }
}
