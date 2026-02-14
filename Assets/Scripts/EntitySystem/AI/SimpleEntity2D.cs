using System;
using UnityEngine;

using Cysharp.Threading.Tasks;
using Random = TheRavine.Extensions.RavineRandom;
using TheRavine.EntityControl;

public class SimpleEntity2D : AEntity
{
    private readonly RavineLogger logger;
    public SimpleEntity2D(RavineLogger logger)
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