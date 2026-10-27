using UnityEngine;
using TheRavine.EntityControl;
public interface IGameEvent { }
public interface IEntityEvent : IGameEvent
{
    AEntity Sender { get; }
}

public struct PickUpEvent : IGameEvent { public long Position; } 
public struct PlaceEvent : IGameEvent { public long Position; }
public struct PickUpResultEvent : IGameEvent { public long Position; public int PrefabId; public int Amount; public bool Ok; }
public struct PlaceResultEvent : IGameEvent { public long Position; public bool Ok; public byte Edit; public byte Placement; }
public struct AimAddition : IGameEvent { public Vector2 Position; }
public struct CameraPlace : IGameEvent { public float scale; public bool flip; }