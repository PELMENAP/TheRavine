using UnityEngine;
using TheRavine.Base;

public class GrassPlayersManger : MonoBehaviour
{
    [Header("Trample Field")]
    [SerializeField] private ComputeShader trampleCompute;
    [SerializeField] private GrassTrampleSettings trample = new();

    private const string ResourcePath = "Grass/GrassTrample";

    private GrassTrampleField field;

    private void OnEnable()
    {
        ComputeShader compute = trampleCompute != null ? trampleCompute : Resources.Load<ComputeShader>(ResourcePath);
        if (compute == null)
        {
            RavineLog.Warning("Grass trample compute shader not found: grass will not react to creatures");
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

        field = new GrassTrampleField(compute, trample);
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
