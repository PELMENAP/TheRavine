using System.Collections.Generic;
using UnityEngine;

using Cysharp.Threading.Tasks;
using TheRavine.Extensions;

public class SimpleEntityController : MonoBehaviour
{
    private DelayedPerceptron delayedPerceptron;
    public string file;
    public List<Entity2D> entities;

    public bool newmode;
    private async void Start()
    {
        NeuralModelStorage.RegisterFactory(new DelayedPerceptronFactory());
        await UniTask.Delay(100 * RavineRandom.RangeInt(5, 20));
        
        if (newmode)
        {
            BehaviorLoopAsync().Forget();
            return;
        }
        delayedPerceptron = await NeuralModelStorage.LoadAsync<DelayedPerceptron>(file);

        foreach (var item in entities)
        {
            item.SetUp(delayedPerceptron);
        }
    }

    private async UniTaskVoid BehaviorLoopAsync()
    {


        foreach (var item in entities)
        {
            item.SetUpAsNew();
        }

        await UniTask.Yield();
    }
}
