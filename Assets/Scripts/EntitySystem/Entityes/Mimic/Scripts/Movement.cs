using UnityEngine;
using UnityEngine.InputSystem;
using TheRavine.Generator;

public class Movement : MonoBehaviour
{
    [Range(0.5f, 5f)] public float height = 0.8f;
    public float speed = 5f;
    public float velocityLerpCoef = 4f;
    [SerializeField] private InputActionReference moveAction;
    
    private Vector3 velocity;
    private Mimic mimic;
    private MapGenerator mapGenerator;

    private async void Start()
    {
        mimic = GetComponent<Mimic>();
        mapGenerator = await ServiceLocator.WaitUntilServiceReady<MapGenerator>();
    }

    private void Update()
    {
        if (mapGenerator == null || mimic == null) return;
        
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        velocity = Vector3.Lerp(
            velocity,
            new Vector3(input.x, 0f, input.y).normalized * speed,
            velocityLerpCoef * Time.deltaTime);

        mimic.velocity = velocity;

        Vector3 pos = transform.position;
        pos.x += velocity.x * Time.deltaTime;
        pos.z += velocity.z * Time.deltaTime;
        
        float surfaceY = mapGenerator.SampleHeightBilinear(pos.x, pos.z);
        pos.y = Mathf.Lerp(transform.position.y, surfaceY + height, velocityLerpCoef * Time.deltaTime);
        transform.position = pos;
    }
}