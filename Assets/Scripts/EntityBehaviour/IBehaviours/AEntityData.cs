using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AEntityData : MonoBehaviour
{
    protected IPlayerBehaviour behaviourCurrent;
    protected Dictionary<Type, IPlayerBehaviour> behavioursMap;
    protected virtual void Init()
    {
    }
    protected virtual void InitBehaviour()
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
