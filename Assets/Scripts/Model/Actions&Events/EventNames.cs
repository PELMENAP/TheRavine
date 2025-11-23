using UnityEngine;
using TheRavine.EntityControl;
public interface IGameEvent { }
public interface IEntityEvent : IGameEvent
{
    AEntity Sender { get; }
}

public struct PickUpEvent : IGameEvent { public Vector2Int Position; } 
public struct PlaceEvent : IGameEvent { public Vector2Int Position; }
public struct AimAddition : IGameEvent { public Vector2 Position; }