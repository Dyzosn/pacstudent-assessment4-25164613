using UnityEngine;
using Random = UnityEngine.Random;

public class CherryController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Spawn Settings")]
    [SerializeField] private GameObject cherryPrefab;
    [SerializeField] private float spawnDelay = 5f;

    private GameObject currentCherry;
    private float spawnTimer;
    private bool canSpawn = true;

    // Level boundaries based on tunnel entrances
    private const float LEVEL_CENTER_X = 14f;
    private const float LEVEL_CENTER_Y = -4.5f;

    private const float LEVEL_LEFT_EDGE = 0.5f;
    private const float LEVEL_RIGHT_EDGE = 27.5f;
    private const float LEVEL_TOP_EDGE = 10f;
    private const float LEVEL_BOTTOM_EDGE = -18f;

    private const float SPAWN_OFFSET = 3f;

    void Start()
    {
        // Start spawn timer
        spawnTimer = spawnDelay;
    }

    void Update()
    {
        // Handle spawn timing
        if (canSpawn && currentCherry == null)
        {
            spawnTimer -= Time.deltaTime;

            if (spawnTimer <= 0f)
            {
                SpawnCherry();
                spawnTimer = spawnDelay;
            }
        }

        // Move existing cherry
        if (currentCherry != null)
        {
            MoveCherry();
        }
    }

    void SpawnCherry()
    {
        if (cherryPrefab == null) return;

        // Random side: 0=left, 1=right, 2=top, 3=bottom
        int side = Random.Range(0, 4);

        Vector3 spawnPosition = Vector3.zero;
        Vector3 targetPosition = Vector3.zero;

        // Calculate spawn and target based on side
        switch (side)
        {
            case 0: // Left side - spawn outside left, move to outside right
                spawnPosition = new Vector3(LEVEL_LEFT_EDGE - SPAWN_OFFSET, LEVEL_CENTER_Y, 0f);
                targetPosition = new Vector3(LEVEL_RIGHT_EDGE + SPAWN_OFFSET, LEVEL_CENTER_Y, 0f);
                break;
            case 1: // Right side - spawn outside right, move to outside left
                spawnPosition = new Vector3(LEVEL_RIGHT_EDGE + SPAWN_OFFSET, LEVEL_CENTER_Y, 0f);
                targetPosition = new Vector3(LEVEL_LEFT_EDGE - SPAWN_OFFSET, LEVEL_CENTER_Y, 0f);
                break;
            case 2: // Top side - spawn above, move to below
                spawnPosition = new Vector3(LEVEL_CENTER_X, LEVEL_TOP_EDGE + SPAWN_OFFSET, 0f);
                targetPosition = new Vector3(LEVEL_CENTER_X, LEVEL_BOTTOM_EDGE - SPAWN_OFFSET, 0f);
                break;
            case 3: // Bottom side - spawn below, move to above
                spawnPosition = new Vector3(LEVEL_CENTER_X, LEVEL_BOTTOM_EDGE - SPAWN_OFFSET, 0f);
                targetPosition = new Vector3(LEVEL_CENTER_X, LEVEL_TOP_EDGE + SPAWN_OFFSET, 0f);
                break;
        }

        // Instantiate cherry
        currentCherry = Instantiate(cherryPrefab, spawnPosition, Quaternion.identity);

        // Store target in cherry's script component
        CherryMovement cherryMovement = currentCherry.AddComponent<CherryMovement>();
        cherryMovement.Initialize(targetPosition, moveSpeed, this);

        Debug.Log($"Cherry spawned at {spawnPosition}, moving to {targetPosition}");
    }

    void MoveCherry()
    {
        // Movement handled by CherryMovement component
        // Check if cherry is out of camera view
        if (currentCherry != null)
        {
            Vector3 cherryPos = currentCherry.transform.position;

            // Destroy if completely outside level boundaries
            bool outsideHorizontal = cherryPos.x < LEVEL_LEFT_EDGE - SPAWN_OFFSET - 2f ||
                                    cherryPos.x > LEVEL_RIGHT_EDGE + SPAWN_OFFSET + 2f;
            bool outsideVertical = cherryPos.y < LEVEL_BOTTOM_EDGE - SPAWN_OFFSET - 2f ||
                                  cherryPos.y > LEVEL_TOP_EDGE + SPAWN_OFFSET + 2f;

            if (outsideHorizontal || outsideVertical)
            {
                DestroyCherry();
            }
        }
    }

    public void DestroyCherry()
    {
        if (currentCherry != null)
        {
            Destroy(currentCherry);
            currentCherry = null;
            spawnTimer = spawnDelay; // Reset timer for next spawn
        }
    }
}

// Separate component for individual cherry movement
public class CherryMovement : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed;
    private CherryController controller;

    public void Initialize(Vector3 target, float moveSpeed, CherryController cherryController)
    {
        targetPosition = target;
        speed = moveSpeed;
        controller = cherryController;
    }

    void Update()
    {
        // Linear movement towards target
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // Check if reached target
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            // Reached end - destroy
            controller.DestroyCherry();
        }
    }
}