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
            private readonly int generationSize = MapGenerator.generationSize;
            public EndlessLiquids(MapGenerator _generator)
            {
                generator = _generator;
            }
            public async UniTaskVoid UpdateChunk(long position)
            {
                Vector3 newPos = new(
                    (Position2Int.GetX(position) + 0.5f) * generationSize,
                    generator.waterOffset.y,
                    (Position2Int.GetY(position) - 0.5f) * generationSize);

                generator.waterTransform.position = newPos;

                // rippleEffect.OnWaterMoved(
                //     newPos,
                //     new Vector2(generationSize, generationSize));

                await UniTask.CompletedTask;
            }
        }
    }
}
