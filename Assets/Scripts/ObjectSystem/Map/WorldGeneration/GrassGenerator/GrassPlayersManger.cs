using UnityEngine;
using TheRavine.Base;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Trample Field")]
    [SerializeField] private ComputeShader trampleCompute;
    [SerializeField] private GrassTrampleSettings trample = new();

    private GrassTrampleField field;

    private void OnEnable()
    {
        if (trampleCompute == null)
        {
            RavineLog.Warning("Grass trample compute shader is not assigned: grass will not react to creatures");
            return;
        }

        bool grass = true;
        try
        {
            grass = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent().enableGrass;
        }
        catch (System.Exception)
        {
        }
        if (!grass) return;

        field = new GrassTrampleField(trampleCompute, trample);
        ServiceLocator.Register(field);
    }

    private void LateUpdate()
    {
        field?.Tick(SimulationClock.DeltaTime);
    }

    private void OnDisable()
    {
        if (field == null) return;
        ServiceLocator.Services.Unregister(field);
        field.Dispose();
        field = null;
    }
}
