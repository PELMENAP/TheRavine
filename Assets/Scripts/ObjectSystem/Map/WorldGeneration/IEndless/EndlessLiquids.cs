using TheRavine.Extensions;
using UnityEngine;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessLiquids : IEndless
        {
            private readonly MapGenerator generator;

            public EndlessLiquids(MapGenerator _generator)
            {
                generator = _generator;
            }

            public void UpdateChunk(long center)
            {
                float waterXPosition = (Position2Int.GetX(center) + 0.5f) * MapGenerator.chunkSize;
                float waterZPosition = (Position2Int.GetY(center) + 0.5f) * MapGenerator.chunkSize;

                generator.waterTransform.position = new Vector3(
                    waterXPosition,
                    generator.waterOffset.y,
                    waterZPosition);

                RippleStampSystem.Instance.SetWaterPosition(waterXPosition, waterZPosition);
            }

            public void Tick(long deadline) { }

            public void OnChunkDirty(long chunkKey) { }

            public void Dispose() { }
        }
    }
}
