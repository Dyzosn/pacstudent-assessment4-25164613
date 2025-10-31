using UnityEngine;
using System.Collections;

public class PacStudentController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Grid References")]
    [SerializeField] private LevelGenerator levelGenerator;

    [Header("Audio")]
    [SerializeField] private AudioClip movementAudioClip;
    [SerializeField] private AudioClip wallCollisionSFX;
    [SerializeField] private AudioClip pelletEatSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("Wall Collision")]
    [SerializeField] private GameObject wallBumpParticlePrefab;

    [Header("Death")]
    [SerializeField] private GameObject deathParticlePrefab;
    [SerializeField] private float deathAnimationDuration = 2f;

    private KeyCode lastInput;
    private KeyCode currentInput;
    private Vector3 targetPosition;
    private bool isLerping = false;
    private float lerpProgress = 0f;

    private Animator animator;
    private AudioSource movementAudioSource;
    private AudioSource sfxAudioSource;
    private ParticleSystem dustParticles;

    // Grid conversion constants
    private const float GRID_SIZE = 1f;
    private Vector2Int currentGridPosition;
    private Vector3 initialPosition;

    // Wall collision tracking
    private Vector3 lastValidPosition;
    private bool isColliding = false;
    private bool wasBlocked = false;

    // Death state
    private bool isDead = false;
    private bool canMove = true;

    void Start()
    {
        // Get components
        animator = GetComponent<Animator>();

        // Get or create audio sources
        AudioSource[] audioSources = GetComponents<AudioSource>();
        if (audioSources.Length >= 1)
        {
            movementAudioSource = audioSources[0];
        }
        else
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
        }

        // Create separate AudioSource for sound effects
        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;

        dustParticles = GetComponentInChildren<ParticleSystem>();

        // Find level generator if not assigned
        if (levelGenerator == null)
        {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }

        // Store initial position for respawn
        initialPosition = transform.position;

        // Calculate starting grid position
        currentGridPosition = WorldToGrid(transform.position);
        targetPosition = transform.position;

        // Store initial position as last valid
        lastValidPosition = transform.position;

        // Initialise input
        lastInput = KeyCode.None;
        currentInput = KeyCode.None;

        Debug.Log($"PacStudent starting at grid: {currentGridPosition}, world: {transform.position}");
    }

    void Update()
    {
        // Skip update if dead
        if (isDead || !canMove) return;

        // Gather player input
        GatherInput();

        // If not lerping, attempt to start new movement
        if (!isLerping)
        {
            TryMove();
        }
        else
        {
            // Continue lerping to target
            PerformLerp();
        }
    }

    void GatherInput()
    {
        // Store last pressed key
        if (Input.GetKeyDown(KeyCode.W))
        {
            lastInput = KeyCode.W;
            wasBlocked = false;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            lastInput = KeyCode.A;
            wasBlocked = false;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            lastInput = KeyCode.S;
            wasBlocked = false;
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            lastInput = KeyCode.D;
            wasBlocked = false;
        }
    }

    void TryMove()
    {
        Vector2Int nextGridPos;
        bool moved = false;

        // Try lastInput first
        if (lastInput != KeyCode.None)
        {
            nextGridPos = GetNextGridPosition(currentGridPosition, lastInput);

            // Check for teleport first
            if (CheckAndHandleTeleport(ref nextGridPos))
            {
                // Teleported - continue in same direction
                currentInput = lastInput;
                currentGridPosition = nextGridPos;
                wasBlocked = false;
                moved = true;
                return;
            }

            if (IsWalkable(nextGridPos))
            {
                currentInput = lastInput;
                currentGridPosition = nextGridPos;
                StartLerp(GridToWorld(nextGridPos));
                UpdateAnimationDirection(lastInput);
                wasBlocked = false;
                moved = true;
                return;
            }
            else
            {
                if (!wasBlocked)
                {
                    TriggerWallCollisionEffects(lastInput);
                    wasBlocked = true;
                }
            }
        }

        // lastInput failed - try currentInput
        if (currentInput != KeyCode.None)
        {
            nextGridPos = GetNextGridPosition(currentGridPosition, currentInput);

            // Check for teleport
            if (CheckAndHandleTeleport(ref nextGridPos))
            {
                currentGridPosition = nextGridPos;
                wasBlocked = false;
                moved = true;
                return;
            }

            if (IsWalkable(nextGridPos))
            {
                currentGridPosition = nextGridPos;
                StartLerp(GridToWorld(nextGridPos));
                UpdateAnimationDirection(currentInput);
                wasBlocked = false;
                moved = true;
                return;
            }
        }

        // Both failed - stop moving
        if (!moved)
        {
            StopMovement();
        }
    }

    void StartLerp(Vector3 target)
    {
        targetPosition = target;
        isLerping = true;
        lerpProgress = 0f;
        lastValidPosition = transform.position;

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
        }

        if (dustParticles != null && !dustParticles.isPlaying)
        {
            dustParticles.Play();
        }

        if (movementAudioSource != null && movementAudioClip != null)
        {
            if (!movementAudioSource.isPlaying)
            {
                movementAudioSource.clip = movementAudioClip;
                movementAudioSource.loop = true;
                movementAudioSource.Play();
            }
        }
    }

    void PerformLerp()
    {
        lerpProgress += moveSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpProgress);

        if (lerpProgress >= 1f)
        {
            transform.position = targetPosition;
            isLerping = false;
            lerpProgress = 0f;
        }
    }

    void StopMovement()
    {
        isLerping = false;

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
        }

        if (dustParticles != null && dustParticles.isPlaying)
        {
            dustParticles.Stop();
        }

        if (movementAudioSource != null && movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }
    }

    void UpdateAnimationDirection(KeyCode direction)
    {
        if (animator == null) return;

        switch (direction)
        {
            case KeyCode.W:
                animator.SetInteger("Direction", 0);
                break;
            case KeyCode.A:
                animator.SetInteger("Direction", 1);
                break;
            case KeyCode.S:
                animator.SetInteger("Direction", 2);
                break;
            case KeyCode.D:
                animator.SetInteger("Direction", 3);
                break;
        }
    }

    void TriggerWallCollisionEffects(KeyCode direction)
    {
        Vector3 collisionPoint = transform.position;
        switch (direction)
        {
            case KeyCode.W: collisionPoint += Vector3.up * 0.5f; break;
            case KeyCode.A: collisionPoint += Vector3.left * 0.5f; break;
            case KeyCode.S: collisionPoint += Vector3.down * 0.5f; break;
            case KeyCode.D: collisionPoint += Vector3.right * 0.5f; break;
        }

        if (wallBumpParticlePrefab != null)
        {
            Instantiate(wallBumpParticlePrefab, collisionPoint, Quaternion.identity);
        }

        if (sfxAudioSource != null && wallCollisionSFX != null)
        {
            sfxAudioSource.PlayOneShot(wallCollisionSFX);
        }
    }

    bool CheckAndHandleTeleport(ref Vector2Int gridPos)
    {
        if (levelGenerator == null) return false;

        int[,] levelMap = levelGenerator.GetLevelMap();
        int cols = levelMap.GetLength(1);
        int fullMapWidth = cols * 2;

        // Check left edge teleport (going further left)
        if (gridPos.x < 0)
        {
            // Teleport to right edge
            gridPos.x = fullMapWidth - 1;
            transform.position = GridToWorld(gridPos);
            currentGridPosition = gridPos;
            targetPosition = transform.position;

            Debug.Log($"Teleported from left edge to right edge at grid: {gridPos}");
            return true;
        }

        // Check right edge teleport (going further right)
        if (gridPos.x >= fullMapWidth)
        {
            // Teleport to left edge
            gridPos.x = 0;
            transform.position = GridToWorld(gridPos);
            currentGridPosition = gridPos;
            targetPosition = transform.position;

            Debug.Log($"Teleported from right edge to left edge at grid: {gridPos}");
            return true;
        }

        return false;
    }

    IEnumerator DeathSequence()
    {
        isDead = true;
        canMove = false;

        // Stop movement
        StopMovement();

        // Play death animation
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // Play death particle effect
        if (deathParticlePrefab != null)
        {
            Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);
        }

        // Play death sound effect
        if (sfxAudioSource != null && deathSFX != null)
        {
            sfxAudioSource.PlayOneShot(deathSFX);
        }

        Debug.Log("PacStudent death sequence started");

        // Wait for death animation to finish
        yield return new WaitForSeconds(deathAnimationDuration);

        // Check if game over
        if (GameManager.Instance != null && GameManager.Instance.GetLives() <= 0)
        {
            Debug.Log("Game Over - no lives remaining");
            yield break;
        }

        // Respawn
        Respawn();
    }

    void Respawn()
    {
        // Reset position to initial spawn
        transform.position = initialPosition;
        currentGridPosition = WorldToGrid(initialPosition);
        targetPosition = initialPosition;
        lastValidPosition = initialPosition;

        // Reset input
        lastInput = KeyCode.None;
        currentInput = KeyCode.None;

        // Reset animator
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
        }

        // Reset death state
        isDead = false;
        canMove = true;

        // Tell GameManager to respawn all ghosts
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnAll();
        }

        Debug.Log("PacStudent respawned at initial position");
    }

    Vector2Int GetNextGridPosition(Vector2Int current, KeyCode direction)
    {
        switch (direction)
        {
            case KeyCode.W: return new Vector2Int(current.x, current.y - 1);
            case KeyCode.A: return new Vector2Int(current.x - 1, current.y);
            case KeyCode.S: return new Vector2Int(current.x, current.y + 1);
            case KeyCode.D: return new Vector2Int(current.x + 1, current.y);
            default: return current;
        }
    }

    bool IsWalkable(Vector2Int gridPos)
    {
        if (levelGenerator == null) return false;

        int[,] levelMap = levelGenerator.GetLevelMap();
        int rows = levelMap.GetLength(0);
        int cols = levelMap.GetLength(1);

        int mappedX = gridPos.x;
        int mappedY = gridPos.y;

        if (gridPos.x >= cols)
        {
            mappedX = (cols * 2 - 1) - gridPos.x;
        }

        if (gridPos.y >= rows)
        {
            mappedY = (rows * 2 - 2) - gridPos.y;
        }

        if (mappedX < 0 || mappedX >= cols || mappedY < 0 || mappedY >= rows)
        {
            return false;
        }

        int tileType = levelMap[mappedY, mappedX];
        return tileType == 0 || tileType == 5 || tileType == 6;
    }

    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int gridX = Mathf.RoundToInt(worldPos.x - 0.5f);
        int gridY = Mathf.RoundToInt(9.5f - worldPos.y);
        return new Vector2Int(gridX, gridY);
    }

    Vector3 GridToWorld(Vector2Int gridPos)
    {
        float worldX = gridPos.x + 0.5f;
        float worldY = 9.5f - gridPos.y;
        return new Vector3(worldX, worldY, 0f);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            if (isColliding) return;
            isColliding = true;

            isLerping = false;
            StopMovement();

            transform.position = lastValidPosition;
            currentGridPosition = WorldToGrid(lastValidPosition);

            if (wallBumpParticlePrefab != null && collision.contacts.Length > 0)
            {
                Vector3 contactPoint = collision.contacts[0].point;
                Instantiate(wallBumpParticlePrefab, contactPoint, Quaternion.identity);
            }

            if (sfxAudioSource != null && wallCollisionSFX != null)
            {
                sfxAudioSource.PlayOneShot(wallCollisionSFX);
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            isColliding = false;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Skip if dead
        if (isDead) return;

        // Handle pellet collection
        if (other.CompareTag("Pellet"))
        {
            Destroy(other.gameObject);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(10);
            }

            if (sfxAudioSource != null && pelletEatSFX != null)
            {
                sfxAudioSource.PlayOneShot(pelletEatSFX);
            }

            Debug.Log("Pellet collected! +10 points");
        }

        // Handle power pellet collection
        else if (other.CompareTag("PowerPellet"))
        {
            Destroy(other.gameObject);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(50);
                GameManager.Instance.StartGhostScaredMode();
            }

            if (sfxAudioSource != null && pelletEatSFX != null)
            {
                sfxAudioSource.PlayOneShot(pelletEatSFX);
            }

            Debug.Log("Power pellet collected! +50 points");
        }

        // Handle bonus cherry collection
        else if (other.CompareTag("BonusCherry"))
        {
            Destroy(other.gameObject);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(100);
            }

            if (sfxAudioSource != null && pelletEatSFX != null)
            {
                sfxAudioSource.PlayOneShot(pelletEatSFX);
            }

            Debug.Log("Bonus cherry collected! +100 points");
        }

        // Handle ghost collision
        else if (other.CompareTag("Ghost"))
        {
            GhostController ghostController = other.GetComponent<GhostController>();
            if (ghostController != null)
            {
                GameManager.GhostState ghostState = ghostController.GetCurrentState();

                // Check ghost state
                if (ghostState == GameManager.GhostState.Normal)
                {
                    // Normal ghost - PacStudent dies
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.PacStudentDeath();
                    }

                    StartCoroutine(DeathSequence());
                    Debug.Log("Collided with normal ghost - PacStudent dies!");
                }
                else if (ghostState == GameManager.GhostState.Scared ||
                         ghostState == GameManager.GhostState.Recovering)
                {
                    // Scared/Recovering ghost - Ghost dies
                    ghostController.Die();
                    Debug.Log("Ate scared/recovering ghost! +300 points");
                }
                // Dead ghost - no collision effect
            }
        }
    }
}