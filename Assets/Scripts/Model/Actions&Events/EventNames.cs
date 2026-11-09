using UnityEngine;
using TheRavine.EntityControl;
using TheRavine.ViewControl;
public interface IGameEvent { }
public interface IEntityEvent : IGameEvent
{
    AEntity Sender { get; }
}

public struct PickUpEvent : IGameEvent { public long Position; public byte Level; } 
public struct PlaceEvent : IGameEvent { public long Position; public byte Level; }
public struct PickUpResultEvent : IGameEvent { public long Position; public byte Level; public int PrefabId; public int Amount; public bool Ok; }
public struct PlaceResultEvent : IGameEvent { public long Position; public byte Level; public bool Ok; public byte Edit; public byte Placement; }
public struct AimAddition : IGameEvent { public Vector2 Position; }
public struct CameraPlace : IGameEvent { public float scale; public ViewFacing facing; }