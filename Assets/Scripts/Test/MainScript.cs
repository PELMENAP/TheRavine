using UnityEngine;
using TheRavine.Extentions;
using NaughtyAttributes;

public class MainScript : MonoBehaviour
{
    public int squareSize = 5, factor;
    public GameObject prefab;

    [Button]
    void Generate()
    {
        // Генерируем точку при нажатии пробела
        Vector2 point = Extention.GetRandomPointAround(new Vector2(30, 30), 2);
        Debug.Log($"Generated Point: {point}");
        Instantiate(prefab, point, Quaternion.identity);
    }
}
