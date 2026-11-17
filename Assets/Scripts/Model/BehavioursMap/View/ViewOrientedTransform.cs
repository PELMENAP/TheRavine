using UnityEngine;

namespace TheRavine.ViewControl
{
    public enum ViewOrientMode : byte
    {
        Yaw = 0,
        Full = 1
    }

    public sealed class ViewOrientedTransform : MonoBehaviour, IViewOriented
    {
        [SerializeField] private ViewOrientMode mode = ViewOrientMode.Yaw;

        private int viewSlot = -1;

        public int ViewSlot { get => viewSlot; set => viewSlot = value; }

        public ViewOrientMode Mode
        {
            get => mode;
            set
            {
                if (mode == value) return;
                mode = value;
                if (isActiveAndEnabled) OnViewChanged(ViewOrientation.Rotation);
            }
        }

        public static ViewOrientedTransform Attach(GameObject target, ViewOrientMode mode)
        {
            if (!target.TryGetComponent(out ViewOrientedTransform oriented))
                oriented = target.AddComponent<ViewOrientedTransform>();
            oriented.Mode = mode;
            return oriented;
        }

        private void OnEnable() => ViewOrientation.Register(this);

        private void OnDisable() => ViewOrientation.Unregister(this);

        public void OnViewChanged(Quaternion rotation) =>
            transform.rotation = mode == ViewOrientMode.Full ? rotation : ViewOrientation.YawRotation;
    }
}
