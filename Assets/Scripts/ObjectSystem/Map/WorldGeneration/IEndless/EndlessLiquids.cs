using UnityEngine;
using Cysharp.Threading.Tasks;
using TheRavine.Extensions;

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
                generator.waterTransform.position = new(
                    (Position2Int.GetX(position) + 0.5f) * generationSize, 
                    generator.waterOffset.y, 
                    (Position2Int.GetY(position) - 0.5f) * generationSize);
                await UniTask.CompletedTask;
            }
        }
    }
}
