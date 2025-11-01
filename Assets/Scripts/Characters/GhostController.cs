using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GhostController : MonoBehaviour
{
    [Header("Ghost Settings")]
    [SerializeField] private bool useCurrentPositionAsInitial = true;
    [SerializeField] private Vector3 manualInitialPosition;
    [SerializeField] private float normalMoveSpeed = 4.5f;

    [Header("Grid References")]
    [SerializeField] private LevelGenerator levelGenerator;

    private int ghostID;

    private enum MoveDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector2Int currentGridPosition;
    private Vector3 targetPosition;
    private MoveDirection currentDirection = MoveDirection.None;
    private MoveDirection previousDirection = MoveDirection.None;

    private bool isLerping = false;
    private float lerpProgress = 0f;
    private float currentMoveSpeed;

    private Animator animator;
    private GameManager.GhostState currentState = GameManager.GhostState.Normal;
    private bool isDead = false;

    private bool hasExitedSpawn = false;
    private bool isInSpawnArea = true;
    private bool isExitingSpawn = false;
    private Vector3 spawnExitTarget;

    private const float SPAWN_LEFT = 12.0f;
    private const float SPAWN_RIGHT = 16.0f;
    private const float SPAWN_TOP = -3.0f;
    private const float SPAWN_BOTTOM = -6.0f;

    private const float TOP_GAP_Y = -1.5f;
    private const float BOTTOM_GAP_Y = -7.5f;
    private const float GAP_CENTER_X = 14.0f;

    private Transform pacStudentTransform;

    // Behavior thresholds
    private const float RED_FLEE_DISTANCE = 12f;    // Red flees when within 12 units

    // Stuck detection for Blue
    private Queue<Vector2Int> bluePositionHistory = new Queue<Vector2Int>();
    private const int POSITION_HISTORY_SIZE = 6;
    private int blueStuckCounter = 0;

    // Escape mode for Blue
    private bool blueInEscapeMode = false;
    private MoveDirection blueEscapeDirection = MoveDirection.None;
    private Vector2Int blueStuckPosition; // Remember where we got stuck

    void Start()
    {
        animator = GetComponent<Animator>();

        if (useCurrentPositionAsInitial)
        {
            initialPosition = transform.position;
        }
        else
        {
            initialPosition = manualInitialPosition;
        }

        initialRotation = transform.rotation;
        ghostID = DetermineGhostID(initialPosition.x);
        currentGridPosition = WorldToGrid(transform.position);
        targetPosition = transform.position;
        currentMoveSpeed = normalMoveSpeed;

        if (levelGenerator == null)
        {
            levelGenerator = FindFirstObjectByType<LevelGenerator>();
        }

        PacStudentController pacStudent = FindFirstObjectByType<PacStudentController>();
        if (pacStudent != null)
        {
            pacStudentTransform = pacStudent.transform;
        }

        isInSpawnArea = IsInsideSpawnArea(transform.position);
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsGameActive())
        {
            return;
        }

        if (!isDead && animator != null)
        {
            int animatorState = animator.GetInteger("GhostState");
            currentState = (GameManager.GhostState)animatorState;
        }

        UpdateMovementSpeed();

        if (isDead)
        {
            PerformDeadMovement();
            return;
        }

        if (isExitingSpawn)
        {
            PerformSpawnExit();
            return;
        }

        if (!isLerping)
        {
            DecideNextMove();
        }
        else
        {
            PerformLerp();
        }

        UpdateSpawnAreaStatus();
    }

    void UpdateMovementSpeed()
    {
        if (currentState == GameManager.GhostState.Normal)
        {
            currentMoveSpeed = normalMoveSpeed;
        }
        else
        {
            currentMoveSpeed = normalMoveSpeed * 0.5f;
        }
    }

    int DetermineGhostID(float xPos)
    {
        if (Mathf.Abs(xPos - 12.5f) < 0.1f) return 1;
        if (Mathf.Abs(xPos - 13.5f) < 0.1f) return 2;
        if (Mathf.Abs(xPos - 14.5f) < 0.1f) return 3;
        if (Mathf.Abs(xPos - 15.5f) < 0.1f) return 4;
        return 1;
    }

    void DecideNextMove()
    {
        if (isInSpawnArea && !hasExitedSpawn && !isExitingSpawn)
        {
            StartSpawnExit();
            return;
        }

        // Track position history for Blue ghost stuck detection
        if (ghostID == 2)
        {
            Vector2Int currentGrid = WorldToGrid(transform.position);
            bluePositionHistory.Enqueue(currentGrid);
            if (bluePositionHistory.Count > POSITION_HISTORY_SIZE)
            {
                bluePositionHistory.Dequeue();
            }
        }

        List<MoveDirection> validDirections = GetValidDirections();

        if (validDirections.Count == 0)
        {
            return;
        }

        MoveDirection nextDirection;

        if (currentState == GameManager.GhostState.Scared ||
            currentState == GameManager.GhostState.Recovering)
        {
            nextDirection = DecideGhost1Move(validDirections);
        }
        else
        {
            switch (ghostID)
            {
                case 1:
                    nextDirection = DecideGhost1Move(validDirections);
                    break;
                case 2:
                    nextDirection = DecideGhost2Move(validDirections);
                    break;
                case 3:
                    nextDirection = DecideGhost3Move(validDirections);
                    break;
                case 4:
                    nextDirection = DecideGhost4Move(validDirections);
                    break;
                default:
                    nextDirection = validDirections[Random.Range(0, validDirections.Count)];
                    break;
            }
        }

        Vector2Int nextGridPos = GetNextGridPosition(currentGridPosition, nextDirection);
        previousDirection = currentDirection;
        currentDirection = nextDirection;
        currentGridPosition = nextGridPos;
        StartLerp(GridToWorld(nextGridPos), nextDirection);
    }

    // Ghost 1 (Red) - FLEE with exploration when safe
    MoveDirection DecideGhost1Move(List<MoveDirection> validDirs)
    {
        if (pacStudentTransform == null || validDirs.Count == 0)
        {
            return validDirs[Random.Range(0, validDirs.Count)];
        }

        Vector3 currentPos = transform.position;
        Vector3 pacPos = pacStudentTransform.position;
        float distanceToPac = Vector3.Distance(currentPos, pacPos);

        // Mode 1: Active flee when PacStudent is close
        if (distanceToPac < RED_FLEE_DISTANCE)
        {
            return FleeWithHybridScoring(validDirs, currentPos, pacPos);
        }

        // Mode 2: Patrol/explore when safe distance
        // Move towards general area but not aggressively
        return PatrolAwayFromTarget(validDirs, currentPos, pacPos);
    }

    // Smart flee using hybrid scoring
    MoveDirection FleeWithHybridScoring(List<MoveDirection> validDirs, Vector3 currentPos, Vector3 targetPos)
    {
        MoveDirection bestDirection = MoveDirection.None;
        float bestScore = float.MinValue;

        Vector2 awayFromTarget = new Vector2(currentPos.x - targetPos.x, currentPos.y - targetPos.y).normalized;

        foreach (MoveDirection dir in validDirs)
        {
            Vector2Int nextGrid = GetNextGridPosition(currentGridPosition, dir);
            Vector3 nextPos = GridToWorld(nextGrid);
            float distance = Vector3.Distance(nextPos, targetPos);

            Vector2 moveVector = GetDirectionVector(dir);
            float alignment = Vector2.Dot(moveVector, awayFromTarget);

            // Scoring: distance + alignment
            float distanceScore = distance / 30f;
            float alignmentScore = (alignment + 1f) / 2f;
            float totalScore = (0.4f * distanceScore * 100f) + (0.6f * alignmentScore * 100f);

            // Bonus for continuing
            if (dir == currentDirection && currentDirection != MoveDirection.None)
            {
                totalScore += 10f;
            }

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestDirection = dir;
            }
        }

        return bestDirection != MoveDirection.None ? bestDirection : validDirs[Random.Range(0, validDirs.Count)];
    }

    // Patrol mode - move around but maintain distance
    MoveDirection PatrolAwayFromTarget(List<MoveDirection> validDirs, Vector3 currentPos, Vector3 targetPos)
    {
        // Prefer continuing current direction (70% chance)
        if (validDirs.Contains(currentDirection) && currentDirection != MoveDirection.None && Random.value < 0.7f)
        {
            return currentDirection;
        }

        // Otherwise, pick direction that maintains safe distance
        List<MoveDirection> goodDirs = new List<MoveDirection>();
        float currentDistance = Vector3.Distance(currentPos, targetPos);

        foreach (MoveDirection dir in validDirs)
        {
            Vector2Int nextGrid = GetNextGridPosition(currentGridPosition, dir);
            Vector3 nextPos = GridToWorld(nextGrid);
            float newDistance = Vector3.Distance(nextPos, targetPos);

            // Accept directions that don't get too close
            if (newDistance >= currentDistance * 0.9f)
            {
                goodDirs.Add(dir);
            }
        }

        if (goodDirs.Count > 0)
        {
            return goodDirs[Random.Range(0, goodDirs.Count)];
        }

        return validDirs[Random.Range(0, validDirs.Count)];
    }

    // Ghost 2 (Blue) - ALWAYS CHASE with escape until intersection
    MoveDirection DecideGhost2Move(List<MoveDirection> validDirs)
    {
        if (pacStudentTransform == null || validDirs.Count == 0)
        {
            return validDirs[Random.Range(0, validDirs.Count)];
        }

        // Declare these once at the start
        Vector3 currentPos = transform.position;
        Vector3 pacPos = pacStudentTransform.position;

        // If in escape mode, continue until intersection or far from stuck position
        if (blueInEscapeMode)
        {
            // Check if we've reached an intersection (3+ WALKABLE directions, not filtered)
            // Need to count walkable before backstep filter
            int walkableCount = 0;
            List<MoveDirection> allDirs = new List<MoveDirection>
            {
                MoveDirection.Up, MoveDirection.Down, MoveDirection.Left, MoveDirection.Right
            };

            foreach (MoveDirection dir in allDirs)
            {
                Vector2Int nextPos = GetNextGridPosition(currentGridPosition, dir);
                Vector3 nextWorldPos = GridToWorld(nextPos);

                // Don't allow re-entry to spawn
                if (!isDead && hasExitedSpawn && IsInsideSpawnArea(nextWorldPos))
                {
                    continue;
                }

                if (IsWalkable(nextPos))
                {
                    walkableCount++;
                }
            }

            bool reachedIntersection = walkableCount >= 3;

            // Check if we're far from stuck position
            Vector2Int currentGrid = WorldToGrid(transform.position);
            float distanceFromStuck = Vector2Int.Distance(currentGrid, blueStuckPosition);
            bool farFromStuck = distanceFromStuck >= 5f;

            // Exit escape mode if reached intersection OR far from stuck
            if (reachedIntersection || farFromStuck)
            {
                blueInEscapeMode = false;
                bluePositionHistory.Clear();
                blueStuckCounter = 0;

                // At intersection, FORCE A TURN (not continue straight)
                if (reachedIntersection)
                {
                    // Get perpendicular directions to escape (force turn)
                    List<MoveDirection> turnDirections = new List<MoveDirection>();

                    if (blueEscapeDirection == MoveDirection.Up || blueEscapeDirection == MoveDirection.Down)
                    {
                        // Was moving vertical, force horizontal turn
                        if (validDirs.Contains(MoveDirection.Left)) turnDirections.Add(MoveDirection.Left);
                        if (validDirs.Contains(MoveDirection.Right)) turnDirections.Add(MoveDirection.Right);
                    }
                    else if (blueEscapeDirection == MoveDirection.Left || blueEscapeDirection == MoveDirection.Right)
                    {
                        // Was moving horizontal, force vertical turn
                        if (validDirs.Contains(MoveDirection.Up)) turnDirections.Add(MoveDirection.Up);
                        if (validDirs.Contains(MoveDirection.Down)) turnDirections.Add(MoveDirection.Down);
                    }

                    // Further filter: remove direction back towards stuck
                    if (turnDirections.Count > 1)
                    {
                        Vector2Int intersectionGrid = WorldToGrid(transform.position);
                        Vector2 awayFromStuck = new Vector2(
                            intersectionGrid.x - blueStuckPosition.x,
                            intersectionGrid.y - blueStuckPosition.y
                        ).normalized;

                        // Pick turn direction that goes AWAY from stuck
                        MoveDirection bestTurn = MoveDirection.None;
                        float bestAlignment = -1f;

                        foreach (MoveDirection dir in turnDirections)
                        {
                            Vector2 dirVector = GetDirectionVector(dir);
                            float alignment = Vector2.Dot(dirVector, awayFromStuck);

                            if (alignment > bestAlignment)
                            {
                                bestAlignment = alignment;
                                bestTurn = dir;
                            }
                        }

                        if (bestTurn != MoveDirection.None)
                        {
                            return bestTurn;
                        }
                    }

                    // If only one turn direction, take it
                    if (turnDirections.Count > 0)
                    {
                        return turnDirections[0];
                    }
                }

                // Far from stuck OR no turn available - use normal hybrid chase
                return ChaseWithHybridScoring(validDirs, currentPos, pacPos);
            }

            // Still escaping - try to continue in escape direction
            if (validDirs.Contains(blueEscapeDirection))
            {
                return blueEscapeDirection;
            }
            else
            {
                // Can't continue escape (hit wall) - pick perpendicular
                MoveDirection alternative = GetPerpendicularToDirection(blueEscapeDirection, validDirs);
                if (alternative != MoveDirection.None)
                {
                    // Update escape direction to new perpendicular
                    blueEscapeDirection = alternative;
                    return alternative;
                }

                // Last resort - pick any valid
                blueInEscapeMode = false;
                return validDirs[Random.Range(0, validDirs.Count)];
            }
        }

        // Detect if Blue is stuck (oscillating between same positions)
        bool isStuck = DetectBlueStuck();

        // If stuck, initiate escape mode
        if (isStuck)
        {
            blueStuckCounter++;

            // Save current position as stuck position
            blueStuckPosition = WorldToGrid(transform.position);

            // Determine escape direction
            MoveDirection escapeDir = MoveDirection.None;

            if (blueStuckCounter >= 3)
            {
                // After 3 stuck detections, try random
                escapeDir = validDirs[Random.Range(0, validDirs.Count)];
            }
            else
            {
                // First 2 stuck detections, try perpendicular
                escapeDir = GetPerpendicularToTarget(validDirs, currentPos, pacPos);
                if (escapeDir == MoveDirection.None)
                {
                    escapeDir = validDirs[Random.Range(0, validDirs.Count)];
                }
            }

            // Activate escape mode
            blueInEscapeMode = true;
            blueEscapeDirection = escapeDir;

            return escapeDir;
        }
        else
        {
            // Not stuck, reset counter
            blueStuckCounter = 0;
        }

        // Normal aggressive hybrid chase scoring
        return ChaseWithHybridScoring(validDirs, currentPos, pacPos);
    }

    // Get direction perpendicular to given direction
    MoveDirection GetPerpendicularToDirection(MoveDirection dir, List<MoveDirection> validDirs)
    {
        List<MoveDirection> perpendiculars = new List<MoveDirection>();

        if (dir == MoveDirection.Up || dir == MoveDirection.Down)
        {
            // Vertical - try horizontal
            if (validDirs.Contains(MoveDirection.Left)) perpendiculars.Add(MoveDirection.Left);
            if (validDirs.Contains(MoveDirection.Right)) perpendiculars.Add(MoveDirection.Right);
        }
        else if (dir == MoveDirection.Left || dir == MoveDirection.Right)
        {
            // Horizontal - try vertical
            if (validDirs.Contains(MoveDirection.Up)) perpendiculars.Add(MoveDirection.Up);
            if (validDirs.Contains(MoveDirection.Down)) perpendiculars.Add(MoveDirection.Down);
        }

        if (perpendiculars.Count > 0)
        {
            return perpendiculars[Random.Range(0, perpendiculars.Count)];
        }

        return MoveDirection.None;
    }

    // Detect if Blue is stuck in local minimum
    bool DetectBlueStuck()
    {
        if (bluePositionHistory.Count < POSITION_HISTORY_SIZE)
        {
            return false; // Not enough data
        }

        // Check if oscillating between same 2-3 positions
        Vector2Int[] positions = bluePositionHistory.ToArray();
        List<Vector2Int> uniquePositions = new List<Vector2Int>();

        foreach (Vector2Int pos in positions)
        {
            if (!uniquePositions.Contains(pos))
            {
                uniquePositions.Add(pos);
            }
        }

        // If only visiting 2-3 positions in last 6 moves, we're stuck
        return uniquePositions.Count <= 3;
    }

    // Get direction perpendicular to target (to escape stuck corner)
    MoveDirection GetPerpendicularToTarget(List<MoveDirection> validDirs, Vector3 currentPos, Vector3 targetPos)
    {
        Vector2 toTarget = new Vector2(targetPos.x - currentPos.x, targetPos.y - currentPos.y).normalized;

        // Get perpendicular vectors (rotate 90 degrees)
        Vector2 perp1 = new Vector2(-toTarget.y, toTarget.x);  // 90 degrees counterclockwise
        Vector2 perp2 = new Vector2(toTarget.y, -toTarget.x);  // 90 degrees clockwise

        MoveDirection bestPerp = MoveDirection.None;
        float bestAlignment = -1f;

        foreach (MoveDirection dir in validDirs)
        {
            Vector2 moveVec = GetDirectionVector(dir);

            // Check alignment with perpendicular vectors
            float align1 = Vector2.Dot(moveVec, perp1);
            float align2 = Vector2.Dot(moveVec, perp2);
            float maxAlign = Mathf.Max(align1, align2);

            if (maxAlign > bestAlignment)
            {
                bestAlignment = maxAlign;
                bestPerp = dir;
            }
        }

        return bestPerp;
    }

    // Smart chase using hybrid scoring
    MoveDirection ChaseWithHybridScoring(List<MoveDirection> validDirs, Vector3 currentPos, Vector3 targetPos)
    {
        MoveDirection bestDirection = MoveDirection.None;
        float bestScore = float.MinValue;

        Vector2 towardsTarget = new Vector2(targetPos.x - currentPos.x, targetPos.y - currentPos.y).normalized;

        foreach (MoveDirection dir in validDirs)
        {
            Vector2Int nextGrid = GetNextGridPosition(currentGridPosition, dir);
            Vector3 nextPos = GridToWorld(nextGrid);
            float distance = Vector3.Distance(nextPos, targetPos);

            Vector2 moveVector = GetDirectionVector(dir);
            float alignment = Vector2.Dot(moveVector, towardsTarget);

            // Scoring: closer is better + aligned is better
            float distanceScore = 1f / (distance + 1f);
            float alignmentScore = (alignment + 1f) / 2f;
            float totalScore = (0.4f * distanceScore * 100f) + (0.6f * alignmentScore * 100f);

            // Bonus for continuing
            if (dir == currentDirection && currentDirection != MoveDirection.None)
            {
                totalScore += 10f;
            }

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestDirection = dir;
            }
        }

        return bestDirection != MoveDirection.None ? bestDirection : validDirs[Random.Range(0, validDirs.Count)];
    }

    // Get direction vector for a move
    Vector2 GetDirectionVector(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Up: return new Vector2(0, 1);
            case MoveDirection.Down: return new Vector2(0, -1);
            case MoveDirection.Left: return new Vector2(-1, 0);
            case MoveDirection.Right: return new Vector2(1, 0);
            default: return Vector2.zero;
        }
    }

    // Ghost 3 (Pink) - Pure random
    MoveDirection DecideGhost3Move(List<MoveDirection> validDirs)
    {
        if (validDirs.Count == 0) return MoveDirection.None;

        // Prefer continuing at intersections
        if (validDirs.Count > 2 && validDirs.Contains(currentDirection) &&
            currentDirection != MoveDirection.None && Random.value < 0.7f)
        {
            return currentDirection;
        }

        return validDirs[Random.Range(0, validDirs.Count)];
    }

    // Ghost 4 (Orange) - Clockwise wall-follow
    MoveDirection DecideGhost4Move(List<MoveDirection> validDirs)
    {
        if (validDirs.Count == 0) return MoveDirection.None;

        // In corridors (only 1-2 options), ALWAYS continue if possible
        if (validDirs.Count == 1)
        {
            return validDirs[0];
        }

        if (validDirs.Count == 2)
        {
            // Two options - pick the one that's NOT backstep
            foreach (MoveDirection dir in validDirs)
            {
                if (!IsOppositeDirection(dir, currentDirection))
                {
                    return dir;
                }
            }
            // If both are valid (shouldn't happen), pick first
            return validDirs[0];
        }

        // At intersections (3+ options), use wall-following logic
        bool isNearWall = IsNearOutsideWall();

        if (isNearWall)
        {
            MoveDirection preferredDir = GetClockwiseDirection(validDirs);
            if (preferredDir != MoveDirection.None)
            {
                return preferredDir;
            }
        }

        // Not near wall - head towards one
        MoveDirection towardsWall = GetDirectionTowardsOutsideWall(validDirs);
        if (towardsWall != MoveDirection.None)
        {
            return towardsWall;
        }

        // Fallback - prefer continuing
        if (validDirs.Contains(currentDirection) && currentDirection != MoveDirection.None)
        {
            return currentDirection;
        }

        return validDirs[Random.Range(0, validDirs.Count)];
    }

    bool IsNearOutsideWall()
    {
        Vector3 pos = transform.position;
        const float THRESHOLD = 2.5f;

        return pos.x <= 0.5f + THRESHOLD || pos.x >= 27.5f - THRESHOLD ||
               pos.y >= 9.5f - THRESHOLD || pos.y <= -18.5f + THRESHOLD;
    }

    MoveDirection GetClockwiseDirection(List<MoveDirection> validDirs)
    {
        Vector3 pos = transform.position;
        const float CHECK = 3.0f;

        bool nearLeft = pos.x <= 0.5f + CHECK;
        bool nearRight = pos.x >= 27.5f - CHECK;
        bool nearTop = pos.y >= 9.5f - CHECK;
        bool nearBottom = pos.y <= -18.5f + CHECK;

        // Corners with explicit priority
        if (nearTop && nearRight)
        {
            if (validDirs.Contains(MoveDirection.Down)) return MoveDirection.Down;
            if (validDirs.Contains(MoveDirection.Left)) return MoveDirection.Left;
        }
        else if (nearRight && nearBottom)
        {
            if (validDirs.Contains(MoveDirection.Left)) return MoveDirection.Left;
            if (validDirs.Contains(MoveDirection.Up)) return MoveDirection.Up;
        }
        else if (nearBottom && nearLeft)
        {
            if (validDirs.Contains(MoveDirection.Up)) return MoveDirection.Up;
            if (validDirs.Contains(MoveDirection.Right)) return MoveDirection.Right;
        }
        else if (nearLeft && nearTop)
        {
            if (validDirs.Contains(MoveDirection.Right)) return MoveDirection.Right;
            if (validDirs.Contains(MoveDirection.Down)) return MoveDirection.Down;
        }
        // Straight walls
        else if (nearTop && validDirs.Contains(MoveDirection.Right)) return MoveDirection.Right;
        else if (nearRight && validDirs.Contains(MoveDirection.Down)) return MoveDirection.Down;
        else if (nearBottom && validDirs.Contains(MoveDirection.Left)) return MoveDirection.Left;
        else if (nearLeft && validDirs.Contains(MoveDirection.Up)) return MoveDirection.Up;

        return MoveDirection.None;
    }

    MoveDirection GetDirectionTowardsOutsideWall(List<MoveDirection> validDirs)
    {
        Vector3 pos = transform.position;

        float distLeft = pos.x - 0.5f;
        float distRight = 27.5f - pos.x;
        float distTop = 9.5f - pos.y;
        float distBottom = pos.y - (-18.5f);

        float minDist = Mathf.Min(distLeft, distRight, distTop, distBottom);
        MoveDirection towardsWall = MoveDirection.None;

        if (minDist == distLeft) towardsWall = MoveDirection.Left;
        else if (minDist == distRight) towardsWall = MoveDirection.Right;
        else if (minDist == distTop) towardsWall = MoveDirection.Up;
        else if (minDist == distBottom) towardsWall = MoveDirection.Down;

        if (validDirs.Contains(towardsWall))
        {
            return towardsWall;
        }

        return MoveDirection.None;
    }

    List<MoveDirection> GetValidDirections()
    {
        List<MoveDirection> validDirs = new List<MoveDirection>();
        List<MoveDirection> allDirs = new List<MoveDirection>
        {
            MoveDirection.Up, MoveDirection.Down, MoveDirection.Left, MoveDirection.Right
        };

        List<MoveDirection> walkableDirs = new List<MoveDirection>();

        // Find all walkable directions
        foreach (MoveDirection dir in allDirs)
        {
            Vector2Int nextPos = GetNextGridPosition(currentGridPosition, dir);
            Vector3 nextWorldPos = GridToWorld(nextPos);

            // Don't allow re-entry to spawn
            if (!isDead && hasExitedSpawn && IsInsideSpawnArea(nextWorldPos))
            {
                continue;
            }

            if (IsWalkable(nextPos))
            {
                walkableDirs.Add(dir);
            }
        }

        // In corridors, allow all walkable (including backstep if needed)
        if (walkableDirs.Count <= 2)
        {
            return walkableDirs;
        }

        // At intersections, avoid backstep
        foreach (MoveDirection dir in walkableDirs)
        {
            if (IsOppositeDirection(dir, previousDirection) && previousDirection != MoveDirection.None)
            {
                continue;
            }

            validDirs.Add(dir);
        }

        // Fallback
        if (validDirs.Count == 0 && walkableDirs.Count > 0)
        {
            return walkableDirs;
        }

        return validDirs;
    }

    void PerformDeadMovement()
    {
        float deadSpeed = normalMoveSpeed * 0.5f;
        Vector3 direction = (initialPosition - transform.position).normalized;
        transform.position += direction * deadSpeed * Time.deltaTime;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            UpdateAnimationDirection(direction.x > 0 ? MoveDirection.Right : MoveDirection.Left);
        }
        else
        {
            UpdateAnimationDirection(direction.y > 0 ? MoveDirection.Up : MoveDirection.Down);
        }

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

        if (ghostID == 1 || ghostID == 3)
        {
            spawnExitTarget = new Vector3(GAP_CENTER_X, TOP_GAP_Y, 0f);
        }
        else
        {
            spawnExitTarget = new Vector3(GAP_CENTER_X, BOTTOM_GAP_Y, 0f);
        }
    }

    void PerformSpawnExit()
    {
        Vector3 direction = (spawnExitTarget - transform.position).normalized;
        float exitSpeed = normalMoveSpeed * 0.5f;
        transform.position += direction * exitSpeed * Time.deltaTime;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            UpdateAnimationDirection(direction.x > 0 ? MoveDirection.Right : MoveDirection.Left);
        }
        else
        {
            UpdateAnimationDirection(direction.y > 0 ? MoveDirection.Up : MoveDirection.Down);
        }

        if (Vector3.Distance(transform.position, spawnExitTarget) < 0.1f)
        {
            transform.position = spawnExitTarget;
            currentGridPosition = WorldToGrid(spawnExitTarget);
            targetPosition = spawnExitTarget;
            isExitingSpawn = false;
            hasExitedSpawn = true;
        }
    }

    void RespawnAtSpawn()
    {
        isDead = false;
        hasExitedSpawn = false;
        isInSpawnArea = true;
        isExitingSpawn = false;

        // Clear Blue stuck detection
        if (ghostID == 2)
        {
            bluePositionHistory.Clear();
            blueStuckCounter = 0;
            blueInEscapeMode = false;
            blueEscapeDirection = MoveDirection.None;
            blueStuckPosition = Vector2Int.zero;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGhostRespawn();
        }

        GameManager.GhostState newState = GameManager.GhostState.Normal;

        if (GameManager.Instance != null && GameManager.Instance.IsGhostScaredActive())
        {
            float timeLeft = GameManager.Instance.GetGhostScaredTimeRemaining();
            newState = timeLeft > 3f ? GameManager.GhostState.Scared : GameManager.GhostState.Recovering;
        }

        SetState(newState);
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

        if (tileType == 8)
        {
            return isExitingSpawn || isDead;
        }

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
    }

    public void ResetToInitialPosition()
    {
        isDead = false;
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        currentGridPosition = WorldToGrid(initialPosition);
        targetPosition = initialPosition;
        currentDirection = MoveDirection.None;
        previousDirection = MoveDirection.None;
        isLerping = false;

        hasExitedSpawn = false;
        isInSpawnArea = true;
        isExitingSpawn = false;

        // Clear Blue stuck detection
        if (ghostID == 2)
        {
            bluePositionHistory.Clear();
            blueStuckCounter = 0;
            blueInEscapeMode = false;
            blueEscapeDirection = MoveDirection.None;
            blueStuckPosition = Vector2Int.zero;
        }

        SetState(GameManager.GhostState.Normal);
    }

    void SetState(GameManager.GhostState newState)
    {
        currentState = newState;

        if (animator != null)
        {
            animator.SetInteger("GhostState", (int)newState);
        }
    }

    public GameManager.GhostState GetCurrentState() => currentState;
    public bool IsDead() => isDead;
    public Vector3 GetInitialPosition() => initialPosition;
    public int GetGhostID() => ghostID;
}