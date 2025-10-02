using UnityEngine;
using EzySlice;
using Plane = EzySlice.Plane;

public class NewBehaviourScript : MonoBehaviour
{
    [Header("Mesh to Slice")]
    public GameObject targetObject;  // объект, который будем резать

    [Header("Cutting Plane (квадрат)")]
    public Transform planeTransform;

    public Material crossSectionMaterial;

    private void Start() 
    {
        DoSlice();
    }

    void DoSlice()
    {
        if (targetObject == null || planeTransform == null)
        {
            Debug.LogWarning("Не назначен targetObject или planeTransform");
            return;
        }

        Vector3 planePos = planeTransform.position;
        Vector3 planeNormal = planeTransform.up;  // или другой вектор нормали

        // Используем extension метод GameObject.Slice(...)
        SlicedHull hull = targetObject.Slice(planePos, planeNormal, crossSectionMaterial);

        if (hull == null)
        {
            Debug.Log("Срез не был выполнен (нет пересечения)");
            return;
        }

        GameObject upper = hull.CreateUpperHull(targetObject, crossSectionMaterial);
        GameObject lower = hull.CreateLowerHull(targetObject, crossSectionMaterial);

        upper.transform.position = targetObject.transform.position;
        upper.transform.rotation = targetObject.transform.rotation;
        lower.transform.position = targetObject.transform.position;
        lower.transform.rotation = targetObject.transform.rotation;

        upper.name = "UpperHull";
        lower.name = "LowerHull";
    }
}
