using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class MobGenerator : MonoBehaviour, ISetAble
{
    [SerializeField] private int count;

    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        callback?.Invoke();
    }

    public void BreakUp()
    {

    }
}
