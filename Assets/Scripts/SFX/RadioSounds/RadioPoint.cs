using UnityEngine;
using System.Threading;
using System.Collections.Generic;


public class RadioPoint : MonoBehaviour
{
    [Header("Radio Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private RadioMood startMood = RadioMood.Normal;
    [SerializeField] private bool followTransform = true;
    
    [Header("Interaction")]
    [SerializeField] private bool canToggle = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private float interactionDistance = 3f;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject visualIndicator;
    [SerializeField] private AudioSource onOffClickSound;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.green;

    private int radioId = -1;
    private bool isPlaying = false;
    private Transform playerTransform;

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        if (playOnStart)
            TurnOn();
        
        UpdateVisualIndicator();
    }

    private void Update()
    {
        if (followTransform && isPlaying && radioId != -1)
        {
            RadioSystem.Instance?.MoveRadio(radioId, transform.position);
        }

        if (canToggle && playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            
            if (distance <= interactionDistance && Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }
    }
    public void TurnOn()
    {
        if (isPlaying) return;
        
        radioId = RadioSystem.Instance.StartRadio(transform);
        
        if (radioId != -1)
        {
            RadioSystem.Instance.SetRadioMood(radioId, startMood);
            isPlaying = true;
            UpdateVisualIndicator();
            PlayClickSound();
            
            Debug.Log($"Radio {gameObject.name} turned ON (ID: {radioId})");
        }
    }

    public void TurnOff()
    {
        if (!isPlaying) return;
        
        if (radioId != -1)
        {
            RadioSystem.Instance.StopRadio(radioId);
            radioId = -1;
            isPlaying = false;
            UpdateVisualIndicator();
            PlayClickSound();
            
            Debug.Log($"Radio {gameObject.name} turned OFF");
        }
    }

    public void Toggle()
    {
        if (isPlaying)
            TurnOff();
        else
            TurnOn();
    }

    public void ChangeMood(RadioMood newMood)
    {
        if (!isPlaying || radioId == -1) return;
        
        RadioSystem.Instance.SetRadioMood(radioId, newMood);
        startMood = newMood;
        
        Debug.Log($"Radio {gameObject.name} mood changed to: {newMood}");
    }

    private void UpdateVisualIndicator()
    {
        if (visualIndicator != null)
            visualIndicator.SetActive(isPlaying);
    }

    private void PlayClickSound()
    {
        if (onOffClickSound != null)
            onOffClickSound.Play();
    }

    private void OnDestroy()
    {
        if (isPlaying && radioId != -1)
        {
            RadioSystem.Instance?.StopRadio(radioId);
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.3f);
    }

    public bool IsPlaying => isPlaying;
    public int RadioId => radioId;
    public RadioMood CurrentMood => startMood;
}
