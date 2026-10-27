using R3;
using System;

namespace TheRavine.EntityControl
{
    public interface IEntity : IDisposable
    {
        bool IsDisposed { get; }
        ReadOnlyReactiveProperty<bool> IsActive { get; }
        Observable<Unit> OnUpdate { get; }
    }
}