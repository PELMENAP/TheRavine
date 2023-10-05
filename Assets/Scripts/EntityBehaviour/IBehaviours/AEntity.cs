using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AEntity : MonoBehaviour
{
    protected IPlayerBehaviour behaviourCurrent;
    protected Dictionary<Type, IPlayerBehaviour> behavioursMap;
    // [SerializeField] protected IControllable controller;

    protected virtual void Init()
    {
        InitBehaviour();
    }

    protected abstract void InitBehaviour();

    protected void SetBehaviour(IPlayerBehaviour newBehaviour)
    {
        if (behaviourCurrent != null)
            behaviourCurrent.Exit();
        behaviourCurrent = newBehaviour;
        behaviourCurrent.Enter();
    }

    protected IPlayerBehaviour GetBehaviour<T>() where T : IPlayerBehaviour => behavioursMap[typeof(T)];
}
