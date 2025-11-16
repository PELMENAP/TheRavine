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
            public void UpdateChunk(Vector2Int Position)
            {
                generator.waterTransform.position = new((Position.x + 0.5f) * generationSize, generator.waterOffset.y, (Position.y - 0.5f) * generationSize);
            }
        }
    }
}
