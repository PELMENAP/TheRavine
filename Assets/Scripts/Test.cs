using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEditor;
using UnityEngine.UI;
using NaughtyAttributes;
using System.Linq;
using System;

public class Test : MonoBehaviour
{
    public Transform player;
    public MapGenerator generator;
    public string test;
    public string enter;

    public Transform watert;
    public MeshFilter water;

    public Transform terraint;
    public MeshFilter terrain;

    public bool isEqual, fisting, isShadow;
    public ControlType control;
    public GameObject prefab;

    [Button]
    private void TEst()
    {
        print(prefab.transform.rotation.z);
    }

    private Vector2 RoundVector(Vector2 vec) => new Vector2((int)vec.x, (int)vec.y);

    private IEnumerator Testing()
    {
        int ID = prefab.GetInstanceID();
        ObjectSystem.inst.PoolManagerBase.CreatePool(prefab, 3);
        int time = 30;
        for (int i = 0; i < time; i++)
        {
            ObjectSystem.inst.PoolManagerBase.Reuse(ID, new Vector2(i * 2, 10));
            yield return new WaitForSeconds(1);
        }
    }

    private void Awake()
    {
        Settings.isShadow = isShadow;
        Settings._controlType = control;
    }
    private void TestFunction()
    {
        double similarity = Extentions.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }
    [Button]

    private void TestFunction1()
    {
    }

    struct Chunk
    {
        public int[,] heightMap;
    }

}
