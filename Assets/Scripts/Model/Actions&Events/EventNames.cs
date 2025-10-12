using UnityEngine;
using TheRavine.EntityControl;
public interface IGameEvent { }
public interface IEntityEvent : IGameEvent
{
    AEntity Sender { get; }
}

public struct PickUpEvent : IGameEvent { public AEntity Sender; public Vector2 Position; } 
public struct PlaceEvent : IGameEvent { public AEntity Sender; public Vector2 Position; }
public struct AimAddition : IGameEvent { public AEntity Sender; public Vector2 Position; }