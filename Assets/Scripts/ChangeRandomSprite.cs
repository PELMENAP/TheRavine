using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeRandomSprite : MonoBehaviour

{
    [SerializeField] private SpriteRenderer render;
    [SerializeField] private Sprite[] randomSpriteList;
    private void OnEnable()
    {
        render.sprite = randomSpriteList[UnityEngine.Random.Range(0, randomSpriteList.Length)];
    }
}
