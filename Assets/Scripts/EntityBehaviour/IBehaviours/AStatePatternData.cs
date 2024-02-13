using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AStatePatternData : MonoBehaviour
{
    protected IPlayerBehaviour behaviourCurrent;
    protected Dictionary<Type, IPlayerBehaviour> behavioursMap;
    protected virtual void Init()
    {
    }

    protected void SetBehaviour(IPlayerBehaviour newBehaviour)
    {
        if (behaviourCurrent != null)
            behaviourCurrent.Exit();
        behaviourCurrent = newBehaviour;
        behaviourCurrent.Enter();
    }
    protected IPlayerBehaviour GetBehaviour<T>() where T : IPlayerBehaviour => behavioursMap[typeof(T)];
}
