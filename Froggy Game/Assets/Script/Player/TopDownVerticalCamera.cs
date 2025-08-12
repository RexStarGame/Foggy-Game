using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TopDownVerticalCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("Offset added to the target before tracking (world units).")]
    public float verticalOffset = 0f;

    [Header("Movement")]
    [Tooltip("How quickly the camera catches up. Smaller = snappier, larger = smoother.")]
    public float smoothTime = 0.15f;

    [Tooltip("Camera won't move while the target stays within this vertical window (world units).")]
    public float deadZoneHeight = 1.5f;

    [Tooltip("How far ahead (in world units) to lead when the player is moving upward/downward.")]
    public float verticalLookAhead = 0.5f;

    [Header("Bounds (optional)")]
    public bool useBounds = false;
    public float minY = -Mathf.Infinity;
    public float maxY = Mathf.Infinity;

    [Header("Lock X")]
    [Tooltip("Locks the camera X to its starting X so it never pans left/right.")]
    public bool lockXToInitial = true;
    public float fixedX = 0f; // used if lockXToInitial is false

    [Header("Teleport Handling")]
    [Tooltip("If the target jumps farther than this in one frame, snap instead of smoothing.")]
    public float snapDistance = 8f;

    float _yVelocity;
    float _initialX;
    float _z;

    void Awake()
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(TopDownVerticalCamera)} has no target assigned.", this);
        }
        _initialX = transform.position.x;
        _z = transform.position.z;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 camPos = transform.position;

        // --- Lock X ---
        float desiredX = lockXToInitial ? _initialX : fixedX;

        // --- Compute desired Y with dead-zone & look-ahead ---
        float targetY = target.position.y + verticalOffset;

        // Simple velocity proxy: how much the target moved this frame (for look-ahead)
        float targetVy = (targetY - (camPos.y)) / Mathf.Max(Time.deltaTime, 0.0001f);

        // Add look-ahead in the direction of motion
        float lookAheadY = Mathf.Sign(targetVy) * verticalLookAhead;

        float targetCenterY = targetY + lookAheadY;
        float halfDZ = Mathf.Max(0f, deadZoneHeight * 0.5f);

        // Only push camera when target exits the dead-zone
        float diff = targetCenterY - camPos.y;
        float desiredY = camPos.y; // default: stay put while inside DZ
        if (diff > halfDZ) desiredY = targetCenterY - halfDZ;
        else if (diff < -halfDZ) desiredY = targetCenterY + halfDZ;

        // Clamp to bounds if enabled
        if (useBounds) desiredY = Mathf.Clamp(desiredY, minY, maxY);

        // Snap if the target teleports far away
        if (Mathf.Abs(desiredY - camPos.y) > snapDistance)
        {
            camPos = new Vector3(desiredX, desiredY, _z);
            _yVelocity = 0f;
        }
        else
        {
            float newY = Mathf.SmoothDamp(camPos.y, desiredY, ref _yVelocity, smoothTime);
            camPos = new Vector3(desiredX, newY, _z);
        }

        transform.position = camPos;
    }

#if UNITY_EDITOR
    // Draw dead-zone as a gizmo in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        float dz = Mathf.Max(0f, deadZoneHeight);
        Vector3 c = Application.isPlaying ? transform.position : new Vector3(transform.position.x, transform.position.y, transform.position.z);
        Vector3 top = new Vector3(c.x, c.y + dz * 0.5f, c.z);
        Vector3 bottom = new Vector3(c.x, c.y - dz * 0.5f, c.z);

        // Draw a vertical capsule-ish marker
        Gizmos.DrawCube(new Vector3(c.x, c.y, c.z), new Vector3(0.1f, dz, 0.1f));
        Gizmos.DrawWireSphere(top, 0.2f);
        Gizmos.DrawWireSphere(bottom, 0.2f);
    }
#endif
}
