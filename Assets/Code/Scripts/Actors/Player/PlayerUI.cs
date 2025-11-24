using UnityEngine;

public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    public Player Player;
    public Crosshair Crosshair;

    [Header("Accuracy / Spread Settings")]
    public float RestingRadius = 10f;
    public float WalkRadius = 25f;
    public float SprintRadius = 45f;
    public float SpreadSpeed = 10f; // How fast it expands/shrinks

    [Header("Interaction Settings")]
    public float InteractionDotRadius = 4f;
    public Color DefaultColor = Color.white;
    public Color InteractionColor = Color.red;

    private float _currentRadius;
    private float _targetRadius;
    private bool _isFocusingInteraction;

    public void Initialize(Player player)
    {
        Player = player;
    }

    private void Start()
    {
        if (Crosshair != null)
        {
            _currentRadius = RestingRadius;
            Crosshair.color = DefaultColor;
        }
    }

    private void Update()
    {
        if (Player.Controller == null || Crosshair == null || Player.Interaction == null) return;

        // 1. Determine State
        // Check if we are looking at a Usable object
        _isFocusingInteraction = Player.Interaction.CurrentUsable != null;

        // 2. Calculate Target Radius
        if (_isFocusingInteraction)
        {
            // Mode: INTERACTION (Small filled dot)
            _targetRadius = InteractionDotRadius;
            Crosshair.SetFilled(true);
            Crosshair.color = Color.Lerp(Crosshair.color, InteractionColor, Time.deltaTime * 10f);
        }
        else
        {
            // Mode: STANDARD (Dynamic Ring)
            Crosshair.SetFilled(false);
            Crosshair.color = Color.Lerp(Crosshair.color, DefaultColor, Time.deltaTime * 10f);

            // Calculate spread based on movement speed
            // Accessing the KCC Motor velocity directly
            float speed = Player.Controller.Motor.Velocity.magnitude;
            
            // Simple logic: If moving fast, expand. If slow, rest.
            // You can map this to WalkStableMoveSpeed / SprintStableMoveSpeed from your Controller
            if (speed > 15f) // Sprint threshold roughly
            {
                _targetRadius = SprintRadius;
            }
            else if (speed > 0.1f)
            {
                _targetRadius = WalkRadius;
            }
            else
            {
                _targetRadius = RestingRadius;
            }
        }

        // 3. Smoothly animate the radius
        // We use Lerp to make it feel "springy" and responsive
        _currentRadius = Mathf.Lerp(_currentRadius, _targetRadius, Time.deltaTime * SpreadSpeed);

        // 4. Apply to the Ring
        // Keep thickness constant (e.g., 2f) so it doesn't get fat when expanding
        Crosshair.SetRadius(_currentRadius, 2f);
    }
}