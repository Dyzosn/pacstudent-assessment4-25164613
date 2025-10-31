using UnityEngine;
using System.Collections;

public class GhostController : MonoBehaviour
{
    [Header("Ghost Settings")]
    [SerializeField] private bool useCurrentPositionAsInitial = true;
    [SerializeField] private Vector3 manualInitialPosition;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Animator animator;
    private GameManager.GhostState currentState = GameManager.GhostState.Normal;
    private bool isDead = false;
    private float deadTimer = 0f;
    private const float DEAD_DURATION = 3f;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Determine initial position
        if (useCurrentPositionAsInitial)
        {
            // Use current scene position
            initialPosition = transform.position;
        }
        else
        {
            // Use manually set position
            initialPosition = manualInitialPosition;
        }

        initialRotation = transform.rotation;

        Debug.Log($"{gameObject.name} spawned at {transform.position}, initial respawn point: {initialPosition}");
    }

    void Update()
    {
        // Update dead timer if ghost is dead
        if (isDead)
        {
            UpdateDeadTimer();
        }

        // Sync current state with animator ONLY if not dead
        // This prevents animator transitions from overriding Dead state
        if (!isDead && animator != null)
        {
            int animatorState = animator.GetInteger("GhostState");
            currentState = (GameManager.GhostState)animatorState;
        }
    }

    public void Die()
    {
        // Ghost has been eaten by PacStudent
        isDead = true;
        deadTimer = DEAD_DURATION;
        currentState = GameManager.GhostState.Dead;

        // Set animator to dead state
        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)GameManager.GhostState.Dead);
        }

        // Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGhostDeath();
        }

        Debug.Log($"{gameObject.name} died! Respawning in {DEAD_DURATION} seconds at {initialPosition}");
    }

    void UpdateDeadTimer()
    {
        deadTimer -= Time.deltaTime;

        if (deadTimer <= 0f)
        {
            Respawn();
        }
    }

    void Respawn()
    {
        // Move to initial position FIRST
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        Debug.Log($"{gameObject.name} moved to respawn position: {initialPosition}");

        // Notify GameManager BEFORE changing state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGhostRespawn();
        }

        // Determine new state based on scared timer
        GameManager.GhostState newState = GameManager.GhostState.Normal;

        if (GameManager.Instance != null)
        {
            bool scaredModeActive = GameManager.Instance.IsGhostScaredActive();
            float scaredTimeLeft = GameManager.Instance.GetGhostScaredTimeRemaining();

            if (scaredModeActive)
            {
                if (scaredTimeLeft > 3f)
                {
                    newState = GameManager.GhostState.Scared;
                }
                else
                {
                    newState = GameManager.GhostState.Recovering;
                }
            }
        }

        // Set new state
        SetState(newState);

        // Reset dead flag LAST
        isDead = false;
        deadTimer = 0f;

        Debug.Log($"{gameObject.name} respawned with state: {newState}");
    }

    public void ResetToInitialPosition()
    {
        // Called when PacStudent dies - reset ghost immediately
        isDead = false;
        deadTimer = 0f;
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        SetState(GameManager.GhostState.Normal);

        Debug.Log($"{gameObject.name} reset to initial position: {initialPosition}");
    }

    void SetState(GameManager.GhostState newState)
    {
        currentState = newState;

        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)newState);
        }
    }

    // Getters
    public GameManager.GhostState GetCurrentState() => currentState;
    public bool IsDead() => isDead;
    public Vector3 GetInitialPosition() => initialPosition;
}