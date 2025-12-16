using System.Collections;
using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    public enum TriggerType
    {
        ButtonInteraction, // Wymaga komponentu Usable i kliknięcia 'E'
        ProximitySensor    // Wymaga Trigger Colladera i podejścia gracza
    }

    [Header("Configuration")]
    public TriggerType OpenMethod = TriggerType.ButtonInteraction;
    public Vector3 SlideOffset = new Vector3(2f, 0, 0); // O ile przesunąć drzwi
    public float AnimationSpeed = 3f;
    public bool StartOpen = false;

    [Header("Auto-Close (Proximity Only)")]
    public bool AutoClose = true;

    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private bool _isOpen;
    private Coroutine _animationCoroutine;
    private Usable _usableComponent;

    private void Start()
    {
        _closedPosition = transform.localPosition;
        _openPosition = _closedPosition + SlideOffset;
        _isOpen = StartOpen;

        transform.localPosition = _isOpen ? _openPosition : _closedPosition;

        // Setup w zależności od trybu
        if (OpenMethod == TriggerType.ButtonInteraction)
        {
            _usableComponent = GetComponent<Usable>();
            if (_usableComponent != null)
            {
                // Podpinamy się pod Event z Usable.cs
                _usableComponent.OnUsed.AddListener(HandleInteraction);
                
                // Aktualizujemy nazwę interakcji
                // (Opcjonalnie, jeśli Usable miałoby pole tekstowe na nazwę)
            }
            else
            {
                Debug.LogWarning($"[SlidingDoor] '{name}' is set to ButtonInteraction but missing 'Usable' component!");
            }
        }
    }

    // --- Logic: Interaction (Button) ---
    // Wywoływane przez event z Usable
    private void HandleInteraction(Player player)
    {
        ToggleDoor();
    }

    // --- Logic: Proximity (Sensor) ---
    private void OnTriggerEnter(Collider other)
    {
        if (OpenMethod != TriggerType.ProximitySensor) return;

        // Sprawdzamy czy to gracz (np. po tagu lub komponencie Player)
        if (other.CompareTag("Player") || other.GetComponent<Player>() != null)
        {
            SetDoorState(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (OpenMethod != TriggerType.ProximitySensor) return;
        if (!AutoClose) return;

        if (other.CompareTag("Player") || other.GetComponent<Player>() != null)
        {
            SetDoorState(false);
        }
    }

    // --- Core Logic ---

    public void ToggleDoor()
    {
        SetDoorState(!_isOpen);
    }

    public void SetDoorState(bool open)
    {
        if (_isOpen == open) return; // Stan się nie zmienił

        _isOpen = open;
        
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(AnimateMotion(_isOpen ? _openPosition : _closedPosition));
        
        // Opcjonalnie: Zaktualizuj stan Usable (np. zablokuj interakcję w trakcie ruchu)
    }

    private IEnumerator AnimateMotion(Vector3 targetPos)
    {
        while (Vector3.Distance(transform.localPosition, targetPos) > 0.01f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, Time.deltaTime * AnimationSpeed);
            yield return null;
        }
        transform.localPosition = targetPos;
        _animationCoroutine = null;
    }
    
    // Wizualizacja offsetu w edytorze
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 target = Application.isPlaying ? _openPosition : transform.localPosition + SlideOffset;
        // Uwaga: Gizmos rysują w World Space, offset jest w Local Space, więc prosta wizualizacja:
        Gizmos.DrawWireCube(transform.parent != null ? transform.parent.TransformPoint(target) : target, Vector3.one * 0.2f);
    }
}