using UnityEngine;

public sealed class VisualCullingComponent : IComponent
{
    private readonly Renderer[] _bodyRenderers;
    private readonly Collider[] _colliders;
    private readonly Mimic _mimic;
    private readonly GameObject _labelObject;

    public bool IsVisible { get; private set; } = true;
    public bool IsDisposed { get; private set; }

    public VisualCullingComponent(GameObject root, GameObject labelObject = null, bool toggleColliders = false)
    {
        _bodyRenderers = root.GetComponentsInChildren<Renderer>(true);
        _colliders = toggleColliders ? root.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
        _mimic = root.GetComponentInChildren<Mimic>(true);
        _labelObject = labelObject;
    }

    public void SetVisible(bool visible)
    {
        if (IsDisposed || IsVisible == visible) return;
        IsVisible = visible;

        for (int i = 0; i < _bodyRenderers.Length; i++)
            _bodyRenderers[i].enabled = visible;

        for (int i = 0; i < _colliders.Length; i++)
            _colliders[i].enabled = visible;

        if (_mimic != null)
            _mimic.enabled = visible;

        if (_labelObject != null)
            _labelObject.SetActive(visible);
    }

    public void Dispose() => IsDisposed = true;
}