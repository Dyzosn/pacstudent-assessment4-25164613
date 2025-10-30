using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Grid References")]
    [SerializeField] private LevelGenerator levelGenerator;

    [Header("Audio")]
    [SerializeField] private AudioClip movementAudioClip;

    private KeyCode lastInput;
    private KeyCode currentInput;
    private Vector3 targetPosition;
    private bool isLerping = false;
    private float lerpProgress = 0f;

    private Animator animator;
    private AudioSource audioSource;
    private ParticleSystem dustParticles;

    // Grid conversion constants - adjust based on your level layout
    private const float GRID_SIZE = 1f;
    private Vector2Int currentGridPosition;

    void Start()
    {
        // Get components
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        dustParticles = GetComponentInChildren<ParticleSystem>();

        // Find level generator
        if (levelGenerator == null)
        {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }

        // Calculate starting grid position
        currentGridPosition = WorldToGrid(transform.position);
        targetPosition = transform.position;

        // Initialise input - no movement at start
        lastInput = KeyCode.None;
        currentInput = KeyCode.None;

        Debug.Log($"PacStudent starting at grid: {currentGridPosition}, world: {transform.position}");
    }

    void Update()
    {
        // Gather player input
        GatherInput();

        // If not lerping, try to start new movement
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
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            lastInput = KeyCode.A;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            lastInput = KeyCode.S;
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            lastInput = KeyCode.D;
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
                moved = true;
                return;
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
                moved = true;
                return;
            }
        }

        // Both failed - stop moving (CRITICAL FIX)
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
        if (audioSource != null && movementAudioClip != null)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = movementAudioClip;
                audioSource.loop = true;
                audioSource.Play();
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
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    void UpdateAnimationDirection(KeyCode direction)
    {
        if (animator == null) return;

        // Set animator parameters based on direction
        // Animation clips handle sprite direction, no need to rotate transform
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

        // Determine which quadrant the position is in
        // Quadrant 1 (top-left): x: 0-13, y: 0-14
        // Quadrant 2 (top-right): x: 14-27, y: 0-14
        // Quadrant 3 (bottom-left): x: 0-13, y: 15-28
        // Quadrant 4 (bottom-right): x: 14-27, y: 15-28

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
            // Bottom half - mirror vertically (skip last row as per specs)
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
        // Each grid cell is 1 unit
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
}