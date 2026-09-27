using System;

public interface IEndless : IDisposable
{
    void UpdateChunk(long center);
    void Tick(long deadline);
    void OnChunkDirty(long chunkKey);
}

public interface IViewDirectional
{
    void SetFacing(int facing);
}
