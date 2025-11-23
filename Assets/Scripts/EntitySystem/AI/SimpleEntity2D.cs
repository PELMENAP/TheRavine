using System;
using UnityEngine;

using Cysharp.Threading.Tasks;
using Random = TheRavine.Extensions.RavineRandom;
using TheRavine.EntityControl;

public class SimpleEntity2D : AEntity
{
    private readonly IRavineLogger logger;
    public SimpleEntity2D(IRavineLogger logger)
    {
        this.logger = logger;
    }
    
    public override void Init()
    {

    }

    public override void UpdateEntityCycle()
    {

    }
}