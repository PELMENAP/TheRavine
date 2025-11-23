using UnityEngine;

public class ChangeRandomSprite : MonoBehaviour

{
    [SerializeField] private SpriteRenderer render;
    [SerializeField] private Sprite[] randomSpriteList;
    private void OnEnable()
    {
        if(randomSpriteList.Length == 0) return;
        render.sprite = randomSpriteList[Random.Range(0, randomSpriteList.Length)];
    }
}
