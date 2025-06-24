using System;
using UnityEngine;

using R3;

public class InputVectorizer
{
    private float _maxHealth = 100f;
    private float _maxEnergy = 100f;
    private const float MaxState = 5f, MaxTime = 24f, MaxCount = 10f;
    private float[] vector = new float[32]; // 20
    private int[] predictedIndexes = new int[4];

    public InputVectorizer(ReactiveProperty<float> maxHealth, ReactiveProperty<float> maxEnergy)
    {
        maxHealth.Subscribe(value => _maxHealth = value);
        maxEnergy.Subscribe(value => _maxEnergy = value);
        _maxHealth = maxHealth.Value;
        _maxEnergy = maxEnergy.Value;
    }

    public int GetVectorSize() => vector.Length;

    public int count = 0;
    public float[] Vectorize(
    float health,
    float energy,
    int   lastActions,
    int   currentState,
    int   timeOfDay,
    bool   lastSuccess, 
    float indanger,
    float timetobreed
    )
    {
        count = (count + 1) % 100;

        predictedIndexes[3] = predictedIndexes[2];
        predictedIndexes[2] = predictedIndexes[1];
        predictedIndexes[1] = predictedIndexes[0];
        predictedIndexes[0] = lastActions;

        vector[0] = 1f - health / _maxHealth;
        vector[1] = 1f - energy / _maxEnergy;
        vector[2] = currentState / MaxState;
        vector[3] = timeOfDay / MaxTime;
        vector[4] = lastSuccess ? 1f : 0f;
        
        for(int i = 0; i < 8; i++)
        {
            vector[5 + i] = 0;

            if(count < 4) continue;
            if(predictedIndexes[0] == i) 
            {
                vector[5 + i] = 1f;
                continue;
            }
            if(predictedIndexes[1] == i)
            {
                vector[5 + i] = 0.75f;
                continue;
            } 
            if(predictedIndexes[2] == i) 
            {
                vector[5 + i] = 0.5f;
                continue;
            }
            if(predictedIndexes[3] == i) 
            {
                vector[5 + i] = 0.25f;
                continue;
            }
        }

        vector[14] = count / MaxCount; 

        vector[15] = indanger;
        vector[16] = indanger;
        vector[17] = indanger;

        vector[18] = timetobreed;
        vector[19] = timetobreed;

        return vector;
    }
}