using UnityEngine;
using TheRavine.Services;
public interface ISetAble
{
    delegate void Callback();
    void SetUp(Callback callback, ServiceLocator locator);
    void BreakUp();
}
