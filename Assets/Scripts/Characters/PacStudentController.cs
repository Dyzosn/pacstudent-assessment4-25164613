using UnityEngine;

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

    [Header("Wall Collision")]
    [SerializeField] private GameObject wallBumpParticlePrefab;

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

    // Wall collision tracking
    private Vector3 lastValidPosition;
    private bool isColliding = false;
    private bool wasBlocked = false;

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

        // Create separate AudioSource for sound effects to avoid conflicts
        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;

        dustParticles = GetComponentInChildren<ParticleSystem>();

        // Find level generator if not assigned
        if (levelGenerator == null)
        {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }

        // Calculate starting grid position
        currentGridPosition = WorldToGrid(transform.position);
        targetPosition = transform.position;

        // Store initial position as last valid
        lastValidPosition = transform.position;

        // Initialise input - no movement at start
        lastInput = KeyCode.None;
        currentInput = KeyCode.None;

        Debug.Log($"PacStudent starting at grid: {currentGridPosition}, world: {transform.position}");
    }

    void Update()
    {
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
        // Store last pressed key - only update if new input detected
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

            if (IsWalkable(nextGridPos))
            {
                // lastInput is valid - use it
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
                // Hit wall with lastInput - trigger effects only on first frame
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

            if (IsWalkable(nextGridPos))
            {
                // currentInput is valid - continue in same direction
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

        // Store current position as last valid before moving
        lastValidPosition = transform.position;

        // Start movement animation
        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
        }

        // Start dust particles
        if (dustParticles != null && !dustParticles.isPlaying)
        {
            dustParticles.Play();
        }

        // Play movement audio (looping)
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
        // Frame-rate independent lerping
        lerpProgress += moveSpeed * Time.deltaTime;

        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpProgress);

        // Check if reached target
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

        // Stop movement audio
        if (movementAudioSource != null && movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }
    }

    void UpdateAnimationDirection(KeyCode direction)
    {
        if (animator == null) return;

        // Set animator parameters based on direction
        switch (direction)
        {
            case KeyCode.W:
                animator.SetInteger("Direction", 0); // Up
                break;
            case KeyCode.A:
                animator.SetInteger("Direction", 1); // Left
                break;
            case KeyCode.S:
                animator.SetInteger("Direction", 2); // Down
                break;
            case KeyCode.D:
                animator.SetInteger("Direction", 3); // Right
                break;
        }
    }

    void TriggerWallCollisionEffects(KeyCode direction)
    {
        // Calculate collision point based on direction
        Vector3 collisionPoint = transform.position;
        switch (direction)
        {
            case KeyCode.W: collisionPoint += Vector3.up * 0.5f; break;
            case KeyCode.A: collisionPoint += Vector3.left * 0.5f; break;
            case KeyCode.S: collisionPoint += Vector3.down * 0.5f; break;
            case KeyCode.D: collisionPoint += Vector3.right * 0.5f; break;
        }

        // Play wall bump particle effect (plays once then destroys itself)
        if (wallBumpParticlePrefab != null)
        {
            Instantiate(wallBumpParticlePrefab, collisionPoint, Quaternion.identity);
        }

        // Play wall collision sound effect
        if (sfxAudioSource != null && wallCollisionSFX != null)
        {
            sfxAudioSource.PlayOneShot(wallCollisionSFX);
        }

        Debug.Log($"Wall collision effects triggered at {collisionPoint}");
    }

    Vector2Int GetNextGridPosition(Vector2Int current, KeyCode direction)
    {
        // Grid coordinates: (0,0) is top-left, x+ is right, y+ is DOWN
        switch (direction)
        {
            case KeyCode.W: return new Vector2Int(current.x, current.y - 1); // Up (y decreases)
            case KeyCode.A: return new Vector2Int(current.x - 1, current.y); // Left
            case KeyCode.S: return new Vector2Int(current.x, current.y + 1); // Down (y increases)
            case KeyCode.D: return new Vector2Int(current.x + 1, current.y); // Right
            default: return current;
        }
    }

    bool IsWalkable(Vector2Int gridPos)
    {
        if (levelGenerator == null) return false;

        // Get base level map (quadrant 1 only)
        int[,] levelMap = levelGenerator.GetLevelMap();
        int rows = levelMap.GetLength(0);
        int cols = levelMap.GetLength(1);

        int mappedX = gridPos.x;
        int mappedY = gridPos.y;

        // Map to quadrant 1 coordinates
        if (gridPos.x >= cols)
        {
            // Right half - mirror horizontally
            mappedX = (cols * 2 - 1) - gridPos.x;
        }

        if (gridPos.y >= rows)
        {
            // Bottom half - mirror vertically
            mappedY = (rows * 2 - 2) - gridPos.y;
        }

        // Check bounds in quadrant 1
        if (mappedX < 0 || mappedX >= cols || mappedY < 0 || mappedY >= rows)
        {
            return false; // Out of bounds
        }

        int tileType = levelMap[mappedY, mappedX];

        // Walkable tiles: 0 (empty), 5 (pellet), 6 (power pellet)
        return tileType == 0 || tileType == 5 || tileType == 6;
    }

    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        // Manual level starts at world position (0.5, 9.5) for grid (0, 0)
        int gridX = Mathf.RoundToInt(worldPos.x - 0.5f);
        int gridY = Mathf.RoundToInt(9.5f - worldPos.y);

        return new Vector2Int(gridX, gridY);
    }

    Vector3 GridToWorld(Vector2Int gridPos)
    {
        // Convert grid coordinates back to world position
        float worldX = gridPos.x + 0.5f;
        float worldY = 9.5f - gridPos.y;

        return new Vector3(worldX, worldY, 0f);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if collided with wall layer
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            // Prevent repeated collision responses
            if (isColliding) return;
            isColliding = true;

            // Stop movement immediately
            isLerping = false;
            StopMovement();

            // Snap back to last valid grid position
            transform.position = lastValidPosition;
            currentGridPosition = WorldToGrid(lastValidPosition);

            // Play wall bump particle at collision point
            if (wallBumpParticlePrefab != null && collision.contacts.Length > 0)
            {
                Vector3 contactPoint = collision.contacts[0].point;
                Instantiate(wallBumpParticlePrefab, contactPoint, Quaternion.identity);
            }

            // Play wall collision sound effect
            if (sfxAudioSource != null && wallCollisionSFX != null)
            {
                sfxAudioSource.PlayOneShot(wallCollisionSFX);
            }

            Debug.Log($"Physical wall collision detected! Reverted to position: {lastValidPosition}");
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        // Reset collision flag when no longer touching wall
        if (collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            isColliding = false;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Handle pellet collection
        if (other.CompareTag("Pellet"))
        {
            Destroy(other.gameObject);

            // Add score via GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(10);
            }

            // Play eating sound effect
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

            Debug.Log("Power pellet collected! +50 points - Ghost scared mode activated!");
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
    }
}