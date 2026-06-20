using Cysharp.Threading.Tasks;
using TheRavine.Extensions;
using UnityEngine;

namespace TheRavine.Generator
{
    namespace EndlessGenerators
    {
        public class EndlessLiquids : IEndless
        {
            private readonly MapGenerator generator;
            private readonly int chunkSize = MapGenerator.chunkSize;
            public EndlessLiquids(MapGenerator _generator)
            {
                generator = _generator;
            }
            public async UniTaskVoid UpdateChunk(long position)
            {
                float waterXPosition = (Position2Int.GetX(position) + 0.5f) * chunkSize;
                float waterZPosition = (Position2Int.GetY(position) - 0.5f) * chunkSize;
                Vector3 newPos = new(
                    waterXPosition,
                    generator.waterOffset.y,
                    waterZPosition);

                generator.waterTransform.position = newPos;
                RippleStampSystem.Instance.SetWaterPosition(waterXPosition, waterZPosition);

                await UniTask.CompletedTask;
            }
        }
    }
}
