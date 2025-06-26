using UnityEngine;
using R3;
using NaughtyAttributes;

using TheRavine.Extensions;

using Cysharp.Threading.Tasks;

public class PerceptronTest : MonoBehaviour
{
    public float health, energy, reward;
    public int currentState, timeOfDay;
    private int predictedIndex;
    public bool lastSuccess;
    public ReactiveProperty<float> maxHealth = new(100), maxEnergy = new(100);

    private InputVectorizer inputVectorizer;
    private DelayedPerceptron delayedPerceptron;
    float[] input;
    private void Start() 
    {
        inputVectorizer = new(maxHealth, maxEnergy);
        delayedPerceptron = new(inputVectorizer.GetVectorSize(), 16, 16, 16, 7);
        For().Forget();
    }

    private async UniTaskVoid For()
    {
        while (true)
        {
            timeOfDay++;
            health -= 0.1f;
            energy -= 0.1f;
            if(timeOfDay > 23) timeOfDay = 0;
            await UniTask.Delay(1000);
        }
    }

    [Button]
    private void MakePrediction()
    {
        input = inputVectorizer.Vectorize(health, energy, predictedIndex, currentState - 1, timeOfDay, lastSuccess, 0.1f, 0.1f);
        predictedIndex = delayedPerceptron.Predict(input) + 1;
        print(predictedIndex);
    }

    [Button]
    private void Train()
    {
        delayedPerceptron.Train(input, predictedIndex, reward);
    }

    [Button]
    private async void Save()
    {
        await DelayedPerceptronStorage.SaveAsync(delayedPerceptron, "default");
    }

    [Button]
    private async void Load()
    {
        delayedPerceptron = await DelayedPerceptronStorage.LoadAsync("default");
    }
}