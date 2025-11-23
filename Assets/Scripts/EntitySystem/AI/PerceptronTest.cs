using UnityEngine;
using R3;
using NaughtyAttributes;

using TheRavine.Extensions;

using Cysharp.Threading.Tasks;

public class PerceptronTest : MonoBehaviour
{
    public float health, energy, reward;
    public int currentState, timeOfDay;
    private float[] predictedIndex;
    public bool lastSuccess;
    public string speech;
    public ReactiveProperty<float> maxHealth = new(100), maxEnergy = new(100);

    private InputVectorizer inputVectorizer;
    private LSTMMemory delayedPerceptron;
    float[] input;
    private void Start() 
    {
        inputVectorizer = new(maxHealth, maxEnergy);
        // delayedPerceptron = new(inputVectorizer.GetVectorSize(), 16, 16, 16, 7);
        delayedPerceptron = new(32, 16);
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
        input = inputVectorizer.Vectorize(health, energy, (int)predictedIndex[0], currentState - 1, timeOfDay, lastSuccess, 0.1f, 0.1f, speech);
        
        
        predictedIndex = delayedPerceptron.Step(input);
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
        await NeuralModelStorage.SaveAsync(delayedPerceptron, "default");
    }

    [Button]
    private async void Load()
    {
        delayedPerceptron = await NeuralModelStorage.LoadAsync<LSTMMemory>("default");
    }
}