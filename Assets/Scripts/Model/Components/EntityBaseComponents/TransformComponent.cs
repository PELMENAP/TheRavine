using TheRavine.EntityControl;
using UnityEngine;
public interface ITransformComponent : IComponent
{
    Vector2 GetEntityPosition();
    Transform GetEntityTransform();
    Vector2 GetModelPosition();
    Transform GetModelTransform();
}

public class TransformComponent : ITransformComponent
{
    private readonly Transform _modelTransform, _entityTransform;

    public TransformComponent(Transform entityTransform, Transform modelTransform)
    {
        _modelTransform = modelTransform;
        _entityTransform = entityTransform;
    }

    public Vector2 GetEntityPosition() => (Vector2)_entityTransform.position;
    public Transform GetEntityTransform() => _entityTransform;
    public Vector2 GetModelPosition() => (Vector2)_modelTransform.position;
    public Transform GetModelTransform() => _modelTransform;

    public void Dispose()
    {
    }
}