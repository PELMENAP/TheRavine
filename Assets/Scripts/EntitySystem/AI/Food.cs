using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class Food : MonoBehaviour
{
    [SerializeField] private float healthValue = 20f;
    [SerializeField] private float energyValue = 30f;
    
    public float HealthValue => healthValue;
    public float EnergyValue => energyValue;
}