namespace TheRavine.Generator
{
    public interface IObstacleLookup
    {
        bool TryGetObstacles(long chunkKey, out ObstacleChunk chunk);
    }
}
