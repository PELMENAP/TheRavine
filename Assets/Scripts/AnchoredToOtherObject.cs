using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchoredToOtherObject : MonoBehaviour
{
    private void OnEnable()
    {
        this.transform.parent = GameObject.Find("InteractiveObject").transform;
    }
}
