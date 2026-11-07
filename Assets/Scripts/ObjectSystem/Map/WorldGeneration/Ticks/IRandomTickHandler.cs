using Unity.Mathematics;

namespace TheRavine.Generator
{
    public struct RandomTickEvent
    {
        public long Chunk;
        public int Cell;
        public int PrefabId;
        public float3 Position;
        public byte Ring;
    }

    public interface IRandomTickHandler
    {
        bool Accepts(int prefabId);
        void OnRandomTick(in RandomTickEvent e, double now);

        bool AcceptsSpread(int prefabId) => false;
        void OnChunkEnter(long chunkKey, ChunkData chunk, long deltaTicks, int ring) { }
    }
}
