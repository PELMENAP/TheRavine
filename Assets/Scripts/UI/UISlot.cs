using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
public class UISlot : MonoBehaviour, IDropHandler
{
    public virtual void OnDrop(PointerEventData eventData)
    {
        var otherItemTransform = eventData.pointerDrag.transform;
        otherItemTransform.SetParent(transform);
        otherItemTransform.localPosition = Vector3.zero;
    }
}
