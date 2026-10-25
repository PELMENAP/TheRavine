using System;
using System.Collections.Generic;
using Unity.Profiling;

using TheRavine.EntityControl.Virology;

public sealed class InfectionCoordinator : IDisposable
{
    private static readonly ProfilerMarker SpreadMarker = new("EntityManager.InfectionSpread");

    private readonly InfectionService _service;

    public InfectionService Service => _service;
    public int Transmissions        => _service.Transmissions;
    public int Recombinations       => _service.Recombinations;
    public int SuperinfectionBlocks => _service.SuperinfectionBlocks;

    public InfectionCoordinator(uint seed) => _service = new InfectionService(seed);

    public void Spread(EntityModel[] batch, int start, int end)
    {
        SpreadMarker.Begin();
        _service.ProcessSpread(batch, start, end);
        SpreadMarker.End();
    }

    public bool SeedRecipe(EntityModel target, ProteinAction[] recipe)
        => _service.InjectRecipe(target, recipe, (uint)RavineRandom.RangeInt(1, int.MaxValue));

    public void SeedRandomStrain(List<EntityModel> entities, int length)
    {
        if (entities.Count == 0) return;
        var victim = entities[RavineRandom.RangeInt(0, entities.Count)];
        _service.InjectStrain(victim, length, (uint)RavineRandom.RangeInt(1, int.MaxValue));
    }

    public void Dispose() => _service.Dispose();
}
