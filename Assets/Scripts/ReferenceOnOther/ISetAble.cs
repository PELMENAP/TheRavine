using UnityEngine;
public interface ISetAble
{
    delegate void Callback();
    void SetUp(Callback callback, ServiceLocator locator);
}
