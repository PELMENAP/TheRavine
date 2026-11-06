using System;
using TheRavine.ViewControl;

public interface IEndless : IDisposable
{
    void UpdateChunk(long center);
    void Tick(long deadline);
    void OnChunkDirty(long chunkKey);
    void OnTerrainChanged(long chunkKey) { }
}

public interface IViewDirectional
{
    void SetFacing(ViewFacing facing);
}
