using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GhostController : MonoBehaviour
{
    [Header("Ghost Settings")]
    [SerializeField] private bool useCurrentPositionAsInitial = true;
    [SerializeField] private Vector3 manualInitialPosition;
    [SerializeField] private float normalMoveSpeed = 4.5f; // 90% of PacStudent's 5.0

    [Header("Grid References")]
    [SerializeField] private LevelGenerator levelGenerator;

    // Ghost ID (1-4) based on starting position
    private int ghostID;

    // Movement direction enum (renamed to avoid conflict with animator parameter)
    private enum MoveDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    // Position tracking
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector2Int currentGridPosition;
    private Vector3 targetPosition;
    private MoveDirection currentDirection = MoveDirection.None;
    private MoveDirection previousDirection = MoveDirection.None;

    // Lerping
    private bool isLerping = false;
    private float lerpProgress = 0f;
    private float currentMoveSpeed;

    // State tracking
    private Animator animator;
    private GameManager.GhostState currentState = GameManager.GhostState.Normal;
    private bool isDead = false;

    // Grid constants (same as PacStudent)
    private const float GRID_SIZE = 1f;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Determine initial position
        if (useCurrentPositionAsInitial)
        {
            initialPosition = transform.position;
        }
        else
        {
            initialPosition = manualInitialPosition;
        }

        initialRotation = transform.rotation;

        // Determine ghost ID based on X position
        ghostID = DetermineGhostID(initialPosition.x);

        // Calculate starting grid position
        currentGridPosition = WorldToGrid(transform.position);
        targetPosition = transform.position;
        currentMoveSpeed = normalMoveSpeed;

        // Find level generator if not assigned
        if (levelGenerator == null)
        {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }

        Debug.Log($"{gameObject.name} (ID: {ghostID}) starting at grid: {currentGridPosition}, world: {transform.position}");
    }

    void Update()
    {
        // Skip update if game not active
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive())
        {
            return;
        }

        // Sync state with animator (unless dead)
        if (!isDead && animator != null)
        {
            int animatorState = animator.GetInteger("GhostState");
            currentState = (GameManager.GhostState)animatorState;
        }

        // Perform movement
        if (!isLerping)
        {
            // Lerp finished - decide next move
            DecideNextMove();
        }
        else
        {
            // Continue lerping
            PerformLerp();
        }
    }

    int DetermineGhostID(float xPos)
    {
        // Determine ID based on spawn X position
        // Red (12.5) = 1, Blue (13.5) = 2, Pink (14.5) = 3, Orange (15.5) = 4
        if (Mathf.Abs(xPos - 12.5f) < 0.1f) return 1;
        if (Mathf.Abs(xPos - 13.5f) < 0.1f) return 2;
        if (Mathf.Abs(xPos - 14.5f) < 0.1f) return 3;
        if (Mathf.Abs(xPos - 15.5f) < 0.1f) return 4;

        Debug.LogWarning($"{gameObject.name} at unexpected position {xPos}, defaulting to ID 1");
        return 1;
    }

    void DecideNextMove()
    {
        // Get list of valid directions (no backstep)
        List<MoveDirection> validDirections = GetValidDirections();

        if (validDirections.Count == 0)
        {
            // No valid moves - stay in place
            Debug.LogWarning($"{gameObject.name} has no valid moves!");
            return;
        }

        // Section 1: Basic random movement for all ghosts (for testing)
        MoveDirection nextDirection = validDirections[Random.Range(0, validDirections.Count)];

        // Calculate next grid position
        Vector2Int nextGridPos = GetNextGridPosition(currentGridPosition, nextDirection);

        // Start moving
        previousDirection = currentDirection;
        currentDirection = nextDirection;
        currentGridPosition = nextGridPos;
        StartLerp(GridToWorld(nextGridPos), nextDirection);
    }

    List<MoveDirection> GetValidDirections()
    {
        List<MoveDirection> validDirs = new List<MoveDirection>();

        // Check all four directions
        foreach (MoveDirection dir in new MoveDirection[] { MoveDirection.Up, MoveDirection.Down, MoveDirection.Left, MoveDirection.Right })
        {
            // Skip backstep (opposite of previous direction)
            if (IsOppositeDirection(dir, previousDirection))
            {
                // Only allow backstep if it's the ONLY option (handled later)
                continue;
            }

            Vector2Int nextPos = GetNextGridPosition(currentGridPosition, dir);

            if (IsWalkable(nextPos))
            {
                validDirs.Add(dir);
            }
        }

        // If no valid directions (cornered), allow backstep
        if (validDirs.Count == 0)
        {
            MoveDirection oppositeDir = GetOppositeDirection(previousDirection);
            Vector2Int backPos = GetNextGridPosition(currentGridPosition, oppositeDir);

            if (IsWalkable(backPos))
            {
                validDirs.Add(oppositeDir);
            }
        }

        return validDirs;
    }

    bool IsOppositeDirection(MoveDirection dir1, MoveDirection dir2)
    {
        if (dir1 == MoveDirection.Up && dir2 == MoveDirection.Down) return true;
        if (dir1 == MoveDirection.Down && dir2 == MoveDirection.Up) return true;
        if (dir1 == MoveDirection.Left && dir2 == MoveDirection.Right) return true;
        if (dir1 == MoveDirection.Right && dir2 == MoveDirection.Left) return true;
        return false;
    }

    MoveDirection GetOppositeDirection(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Up: return MoveDirection.Down;
            case MoveDirection.Down: return MoveDirection.Up;
            case MoveDirection.Left: return MoveDirection.Right;
            case MoveDirection.Right: return MoveDirection.Left;
            default: return MoveDirection.None;
        }
    }

    void StartLerp(Vector3 target, MoveDirection direction)
    {
        targetPosition = target;
        isLerping = true;
        lerpProgress = 0f;

        // Update animator direction
        UpdateAnimationDirection(direction);
    }

    void PerformLerp()
    {
        lerpProgress += currentMoveSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpProgress);

        if (lerpProgress >= 1f)
        {
            // Lerp complete - snap to target
            transform.position = targetPosition;
            isLerping = false;
            lerpProgress = 0f;
        }
    }

    void UpdateAnimationDirection(MoveDirection dir)
    {
        if (animator == null) return;

        // Set Direction parameter (0=Down, 1=Left, 2=Right, 3=Up)
        // Blend Tree parameters must be Float, not Int!
        float directionValue = 0f;

        switch (dir)
        {
            case MoveDirection.Down:
                directionValue = 0f;
                break;
            case MoveDirection.Left:
                directionValue = 1f;
                break;
            case MoveDirection.Right:
                directionValue = 2f;
                break;
            case MoveDirection.Up:
                directionValue = 3f;
                break;
        }

        animator.SetFloat("Direction", directionValue);
    }

    Vector2Int GetNextGridPosition(Vector2Int current, MoveDirection direction)
    {
        switch (direction)
        {
            case MoveDirection.Up: return new Vector2Int(current.x, current.y - 1);
            case MoveDirection.Down: return new Vector2Int(current.x, current.y + 1);
            case MoveDirection.Left: return new Vector2Int(current.x - 1, current.y);
            case MoveDirection.Right: return new Vector2Int(current.x + 1, current.y);
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

        // Mirror logic for full map
        if (gridPos.x >= cols)
        {
            mappedX = (cols * 2 - 1) - gridPos.x;
        }

        if (gridPos.y >= rows)
        {
            mappedY = (rows * 2 - 2) - gridPos.y;
        }

        // Bounds check
        if (mappedX < 0 || mappedX >= cols || mappedY < 0 || mappedY >= rows)
        {
            return false;
        }

        int tileType = levelMap[mappedY, mappedX];

        // Walkable tiles: 0=empty corridor, 5=pellet, 6=power pellet
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

    // Phase 70% methods (keep for compatibility)
    public void Die()
    {
        // Ghost eaten by PacStudent
        isDead = true;
        currentState = GameManager.GhostState.Dead;

        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)GameManager.GhostState.Dead);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGhostDeath();
        }

        Debug.Log($"{gameObject.name} died!");
    }

    public void ResetToInitialPosition()
    {
        // Called when PacStudent dies - reset immediately
        isDead = false;
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        currentGridPosition = WorldToGrid(initialPosition);
        targetPosition = initialPosition;
        currentDirection = MoveDirection.None;
        previousDirection = MoveDirection.None;
        isLerping = false;

        SetState(GameManager.GhostState.Normal);

        Debug.Log($"{gameObject.name} reset to initial position");
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
    public int GetGhostID() => ghostID;
}