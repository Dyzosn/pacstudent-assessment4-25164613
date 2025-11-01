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

    // Movement direction enum
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

    // Section 2: Spawn area management
    private bool hasExitedSpawn = false;
    private bool isInSpawnArea = true;
    private bool isExitingSpawn = false;
    private Vector3 spawnExitTarget;

    // Spawn area boundaries (adjusted so exit gaps are outside)
    private const float SPAWN_LEFT = 12.0f;
    private const float SPAWN_RIGHT = 16.0f;
    private const float SPAWN_TOP = -3.0f;    // Above top exit wall
    private const float SPAWN_BOTTOM = -6.0f; // Above bottom exit wall

    // Exit gap positions (OUTSIDE spawn area)
    private const float TOP_GAP_Y = -1.5f;    // One tile above exit wall
    private const float BOTTOM_GAP_Y = -7.5f; // One tile below exit wall
    private const float GAP_CENTER_X = 14.0f;

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

        // Determine ghost ID
        ghostID = DetermineGhostID(initialPosition.x);

        // Calculate starting grid position
        currentGridPosition = WorldToGrid(transform.position);
        targetPosition = transform.position;
        currentMoveSpeed = normalMoveSpeed;

        // Find level generator
        if (levelGenerator == null)
        {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }

        // Check if starting in spawn
        isInSpawnArea = IsInsideSpawnArea(transform.position);

        Debug.Log($"{gameObject.name} (ID: {ghostID}) at {transform.position}, spawn: {initialPosition}");
    }

    void Update()
    {
        // Skip if game not active
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

        // Update movement speed based on state
        UpdateMovementSpeed();

        // Dead state: Move to spawn
        if (isDead)
        {
            PerformDeadMovement();
            return;
        }

        // Exiting spawn: Special movement
        if (isExitingSpawn)
        {
            PerformSpawnExit();
            return;
        }

        // Normal movement
        if (!isLerping)
        {
            DecideNextMove();
        }
        else
        {
            PerformLerp();
        }

        // Track spawn area
        UpdateSpawnAreaStatus();
    }

    void UpdateMovementSpeed()
    {
        // Normal: 90% PacStudent (4.5)
        // Scared/Recovering/Dead: 50% Normal Ghost Speed (2.25)
        if (currentState == GameManager.GhostState.Normal)
        {
            currentMoveSpeed = normalMoveSpeed; // 4.5
        }
        else if (currentState == GameManager.GhostState.Scared ||
                 currentState == GameManager.GhostState.Recovering ||
                 currentState == GameManager.GhostState.Dead)
        {
            currentMoveSpeed = normalMoveSpeed * 0.5f; // 2.25
        }
    }

    int DetermineGhostID(float xPos)
    {
        if (Mathf.Abs(xPos - 12.5f) < 0.1f) return 1;
        if (Mathf.Abs(xPos - 13.5f) < 0.1f) return 2;
        if (Mathf.Abs(xPos - 14.5f) < 0.1f) return 3;
        if (Mathf.Abs(xPos - 15.5f) < 0.1f) return 4;

        Debug.LogWarning($"{gameObject.name} at {xPos}, defaulting ID 1");
        return 1;
    }

    void DecideNextMove()
    {
        // If in spawn and not exited yet, start exit
        if (isInSpawnArea && !hasExitedSpawn && !isExitingSpawn)
        {
            StartSpawnExit();
            return;
        }

        // Get valid directions
        List<MoveDirection> validDirections = GetValidDirections();

        if (validDirections.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name} no valid moves!");
            return;
        }

        // Random movement
        MoveDirection nextDirection = validDirections[Random.Range(0, validDirections.Count)];

        Vector2Int nextGridPos = GetNextGridPosition(currentGridPosition, nextDirection);

        previousDirection = currentDirection;
        currentDirection = nextDirection;
        currentGridPosition = nextGridPos;
        StartLerp(GridToWorld(nextGridPos), nextDirection);
    }

    // Dead: Move straight to OWN initialPosition
    void PerformDeadMovement()
    {
        float deadSpeed = normalMoveSpeed * 0.5f;

        Vector3 direction = (initialPosition - transform.position).normalized;
        transform.position += direction * deadSpeed * Time.deltaTime;

        // Animation facing
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            UpdateAnimationDirection(direction.x > 0 ? MoveDirection.Right : MoveDirection.Left);
        }
        else
        {
            UpdateAnimationDirection(direction.y > 0 ? MoveDirection.Up : MoveDirection.Down);
        }

        // Check arrival at spawn
        if (Vector3.Distance(transform.position, initialPosition) < 0.2f)
        {
            transform.position = initialPosition;
            transform.rotation = initialRotation;
            currentGridPosition = WorldToGrid(initialPosition);
            targetPosition = initialPosition;

            RespawnAtSpawn();
        }
    }

    void StartSpawnExit()
    {
        isExitingSpawn = true;

        // Ghost 1&3 = top, Ghost 2&4 = bottom
        if (ghostID == 1 || ghostID == 3)
        {
            spawnExitTarget = new Vector3(GAP_CENTER_X, TOP_GAP_Y, 0f);
        }
        else
        {
            spawnExitTarget = new Vector3(GAP_CENTER_X, BOTTOM_GAP_Y, 0f);
        }

        Debug.Log($"{gameObject.name} exiting to {spawnExitTarget}");
    }

    void PerformSpawnExit()
    {
        Vector3 direction = (spawnExitTarget - transform.position).normalized;
        float exitSpeed = normalMoveSpeed * 0.5f;

        transform.position += direction * exitSpeed * Time.deltaTime;

        // Animation
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            UpdateAnimationDirection(direction.x > 0 ? MoveDirection.Right : MoveDirection.Left);
        }
        else
        {
            UpdateAnimationDirection(direction.y > 0 ? MoveDirection.Up : MoveDirection.Down);
        }

        // Check if exited
        if (!IsInsideSpawnArea(transform.position))
        {
            isExitingSpawn = false;
            hasExitedSpawn = true;
            isInSpawnArea = false;

            // Snap to grid
            currentGridPosition = WorldToGrid(transform.position);
            targetPosition = GridToWorld(currentGridPosition);
            transform.position = targetPosition;

            Debug.Log($"{gameObject.name} exited spawn");
        }
    }

    void RespawnAtSpawn()
    {
        isDead = false;
        isInSpawnArea = true;
        hasExitedSpawn = false;
        isExitingSpawn = false;

        // Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGhostRespawn();
        }

        // Determine state based on scared timer
        GameManager.GhostState newState = GameManager.GhostState.Normal;

        if (GameManager.Instance != null && GameManager.Instance.IsGhostScaredActive())
        {
            float timeLeft = GameManager.Instance.GetGhostScaredTimeRemaining();
            newState = timeLeft > 3f ? GameManager.GhostState.Scared : GameManager.GhostState.Recovering;
        }

        SetState(newState);

        Debug.Log($"{gameObject.name} respawned as {newState}");
    }

    bool IsInsideSpawnArea(Vector3 pos)
    {
        return pos.x >= SPAWN_LEFT && pos.x <= SPAWN_RIGHT &&
               pos.y >= SPAWN_BOTTOM && pos.y <= SPAWN_TOP;
    }

    void UpdateSpawnAreaStatus()
    {
        isInSpawnArea = IsInsideSpawnArea(transform.position);
    }

    List<MoveDirection> GetValidDirections()
    {
        List<MoveDirection> validDirs = new List<MoveDirection>();

        foreach (MoveDirection dir in new MoveDirection[] { MoveDirection.Up, MoveDirection.Down, MoveDirection.Left, MoveDirection.Right })
        {
            // No backstep
            if (IsOppositeDirection(dir, previousDirection))
            {
                continue;
            }

            Vector2Int nextPos = GetNextGridPosition(currentGridPosition, dir);

            // Prevent re-entry to spawn (unless dead)
            Vector3 nextWorldPos = GridToWorld(nextPos);
            if (!isDead && hasExitedSpawn && IsInsideSpawnArea(nextWorldPos))
            {
                continue;
            }

            if (IsWalkable(nextPos))
            {
                validDirs.Add(dir);
            }
        }

        // Allow backstep if cornered
        if (validDirs.Count == 0)
        {
            MoveDirection oppositeDir = GetOppositeDirection(previousDirection);
            Vector2Int backPos = GetNextGridPosition(currentGridPosition, oppositeDir);

            Vector3 backWorldPos = GridToWorld(backPos);
            if (isDead || !hasExitedSpawn || !IsInsideSpawnArea(backWorldPos))
            {
                if (IsWalkable(backPos))
                {
                    validDirs.Add(oppositeDir);
                }
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

        UpdateAnimationDirection(direction);
    }

    void PerformLerp()
    {
        lerpProgress += currentMoveSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpProgress);

        if (lerpProgress >= 1f)
        {
            transform.position = targetPosition;
            isLerping = false;
            lerpProgress = 0f;
        }
    }

    void UpdateAnimationDirection(MoveDirection dir)
    {
        if (animator == null) return;

        float directionValue = 0f;

        switch (dir)
        {
            case MoveDirection.Down: directionValue = 0f; break;
            case MoveDirection.Left: directionValue = 1f; break;
            case MoveDirection.Right: directionValue = 2f; break;
            case MoveDirection.Up: directionValue = 3f; break;
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

        // Mirror logic
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

        // Tile 8 (ghost exit wall) has special rules:
        // - Only walkable when exiting spawn OR dead (returning to spawn)
        // - After exiting, becomes a wall (cannot step on door)
        if (tileType == 8)
        {
            return isExitingSpawn || isDead;
        }

        // Normal walkable tiles: 0=empty, 5=pellet, 6=power pellet
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

    public void Die()
    {
        // Eaten by PacStudent - enter dead state immediately
        isDead = true;
        currentState = GameManager.GhostState.Dead;
        isLerping = false;

        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)GameManager.GhostState.Dead);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGhostDeath();
        }

        Debug.Log($"{gameObject.name} died! Returning to {initialPosition}");
    }

    public void ResetToInitialPosition()
    {
        // PacStudent dies - reset all ghosts
        isDead = false;
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        currentGridPosition = WorldToGrid(initialPosition);
        targetPosition = initialPosition;
        currentDirection = MoveDirection.None;
        previousDirection = MoveDirection.None;
        isLerping = false;

        // Reset spawn flags
        hasExitedSpawn = false;
        isInSpawnArea = true;
        isExitingSpawn = false;

        SetState(GameManager.GhostState.Normal);

        Debug.Log($"{gameObject.name} reset");
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