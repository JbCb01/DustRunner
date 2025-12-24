using UnityEngine;
using Unity.Cinemachine;

public class PlayerCamera : MonoBehaviour
{
    [Header("References")]
    public CinemachineCamera VCam;
    public Camera Main;
    
    [Tooltip("The object to apply recoil/shake to. Can be CameraRoot or EquipPoint.")]
    public Transform RecoilTransform; 

    [Header("FOV Kick")]
    public float baseFov = 75f;
    public float sprintFov = 85f;
    public float fovLerpSpeed = 8f;

    [Header("Recoil Springs")]
    [Tooltip("Controls rotation (Pitch, Yaw, Roll)")]
    public SpringState RotationSpring = new SpringState();
    
    [Tooltip("Controls position kickback (Z axis mostly)")]
    public SpringState PositionSpring = new SpringState();

    private bool _sprinting;
    
    // Cache początkowej pozycji, żeby nie nadpisywać offsetu broni/kamery
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;

    // Aktualne wartości wyliczone przez sprężynę
    private Vector3 _currentRotationRecoil;
    private Vector3 _targetRotationRecoil;
    private Vector3 _currentPositionRecoil;
    private Vector3 _targetPositionRecoil;

    private void Awake()
    {
        if (Main == null) Main = Camera.main;
        
        // Inicjalizacja Sprężyn
        RotationSpring.Initialize();
        PositionSpring.Initialize();

        // Zapamiętaj "Zero" dla obiektu, którym trzęsiemy
        if (RecoilTransform != null)
        {
            _initialLocalPosition = RecoilTransform.localPosition;
            _initialLocalRotation = RecoilTransform.localRotation;
        }
        else
        {
            // Fallback: jeśli nic nie przypisałeś, szukamy CameraEffects lub używamy siebie (niezalecane dla KCC)
            Debug.LogWarning("[PlayerCamera] No RecoilTransform assigned via Inspector.");
        }
    }

    private void Start()
    {
        if (VCam != null) VCam.Lens.FieldOfView = baseFov;
    }

    private void LateUpdate()
    {
        HandleFov();
        HandleRecoilPhysics();
    }

    // --- Logic ---

    private void HandleFov()
    {
        if (!VCam) return;
        float targetFov = _sprinting ? sprintFov : baseFov;
        VCam.Lens.FieldOfView = Mathf.Lerp(VCam.Lens.FieldOfView, targetFov, 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime));
    }

    private void HandleRecoilPhysics()
    {
        // 1. Aktualizacja matematyki sprężyn (niezależna od obiektu)
        float dt = Time.deltaTime;
        float rotVal = RotationSpring.Update(dt);
        float posVal = PositionSpring.Update(dt);

        // 2. Interpolacja wektorów recoilu
        _currentRotationRecoil = Vector3.Slerp(_currentRotationRecoil, _targetRotationRecoil * rotVal, dt * 20f);
        _currentPositionRecoil = Vector3.Lerp(_currentPositionRecoil, _targetPositionRecoil * posVal, dt * 20f);

        // 3. Aplikowanie na transform (FIX: Dodajemy do Initial, a nie nadpisujemy)
        if (RecoilTransform != null)
        {
            // Rotation: Initial * Recoil (kolejność mnożenia Quaternionów ma znaczenie!)
            RecoilTransform.localRotation = _initialLocalRotation * Quaternion.Euler(_currentRotationRecoil);
            
            // Position: Initial + Recoil
            RecoilTransform.localPosition = _initialLocalPosition + _currentPositionRecoil;
        }

        // 4. Wygaszanie celu (powrót do równowagi)
        _targetRotationRecoil = Vector3.Lerp(_targetRotationRecoil, Vector3.zero, dt * 2f);
        _targetPositionRecoil = Vector3.Lerp(_targetPositionRecoil, Vector3.zero, dt * 2f);
    }

    public void AddRecoil(Vector3 rotationKick, Vector3 positionKick, float randomness = 0.2f)
    {
        // Losowość (Jitter)
        float randomYaw = Random.Range(-randomness, randomness) * rotationKick.y;
        float randomRoll = Random.Range(-randomness, randomness) * rotationKick.z;

        // Ustawienie celu wychylenia (-X to góra w Unity EulerAngles)
        _targetRotationRecoil += new Vector3(-rotationKick.x, randomYaw, randomRoll);
        
        // Pozycja (kopnięcie w tył to zazwyczaj -Z lub +Z zależnie od setupu, tutaj zakładam -Z)
        _targetPositionRecoil += new Vector3(
            Random.Range(-positionKick.x, positionKick.x), 
            Random.Range(0, positionKick.y), 
            positionKick.z
        );

        // Dodanie energii do układu sprężyn
        RotationSpring.AddForce(10f);
        PositionSpring.AddForce(10f);
    }

    public void SetSprinting(bool sprinting) => _sprinting = sprinting;

    public Vector3 GetPlanarForward()
    {
        if (!Main) return transform.forward;
        Vector3 fwd = Main.transform.forward;
        fwd.y = 0f;
        return fwd.normalized;
    }

    public Vector3 GetPlanarRight()
    {
        if (!Main) return transform.right;
        Vector3 right = Main.transform.right;
        right.y = 0f;
        return right.normalized;
    }
}

[System.Serializable]
public class SpringState
{
    public float Stiffness = 150f;
    public float Damping = 10f;
    public float Mass = 1f;

    private float _currentValue;
    private float _velocity;

    public void Initialize()
    {
        _currentValue = 0f;
        _velocity = 0f;
    }

    public void AddForce(float force)
    {
        _velocity += force / Mass;
    }

    public float Update(float deltaTime)
    {
        // Simple Harmonic Motion: F = -kx - cv
        float force = -Stiffness * _currentValue - Damping * _velocity;
        _velocity += force * deltaTime;
        _currentValue += _velocity * deltaTime;
        return _currentValue;
    }
}