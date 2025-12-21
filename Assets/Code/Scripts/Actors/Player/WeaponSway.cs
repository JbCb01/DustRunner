using UnityEngine;

public class WeaponSway : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float swayAmount = 2f;
    [SerializeField] private float maxSwayAmount = 5f;
    [SerializeField] private float smoothAmount = 6f;

    private Quaternion _initialRotation;
    private Player _player;

    public void Initialize(Player player)
    {
        _player = player;
        _initialRotation = transform.localRotation;
    }

    private void Update()
    {
        if (_player == null || _player.Input == null) return;

        UpdateSway();
    }

    private void UpdateSway()
    {
        // Pobieramy Raw Delta z Inputu (pobrane z Twojego PlayerControls/Input System)
        Vector2 mouseDelta = _player.Input.Player.Look.ReadValue<Vector2>();

        // Obliczamy docelową rotację na podstawie ruchu myszy
        float movementX = -mouseDelta.x * swayAmount;
        float movementY = -mouseDelta.y * swayAmount;

        // Ograniczamy maksymalne wychylenie
        movementX = Mathf.Clamp(movementX, -maxSwayAmount, maxSwayAmount);
        movementY = Mathf.Clamp(movementY, -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = Quaternion.Euler(movementY, movementX, 0);

        // Płynnie wracamy do rotacji początkowej lub wychylamy się
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation * _initialRotation, Time.deltaTime * smoothAmount);
    }
}