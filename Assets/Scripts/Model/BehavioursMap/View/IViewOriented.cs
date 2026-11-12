using UnityEngine;

namespace TheRavine.ViewControl
{
    public interface IViewOriented
    {
        int ViewSlot { get; set; }
        void OnViewChanged(Quaternion rotation);
    }
}
