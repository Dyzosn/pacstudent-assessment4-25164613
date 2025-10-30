using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    // The 2D array from specs
    private int[,] levelMap =
    {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0},
    };

    // Test map for testing different layouts (To test this please check both of "Generate New Map" and "Use Test Map" in inspector)
    private int[,] testMap =
    {
        {1,2,2,7},
        {2,5,5,4},
        {2,5,5,4},
        {1,2,2,3}
    };

    // Current map being used for generation
    private int[,] currentMap;

    // Prefabs for each tile type
    public GameObject[] tilePrefabs = new GameObject[9]; // Index matches tile type

    // Level management
    public GameObject manualLevelParent;
    public Transform proceduralLevelParent;

    // Control flags
    public bool generateNewMap = false;
    public bool useTestMap = false;

    void Start()
    {
        // Only generate procedural level if flag is enabled
        if (generateNewMap)
        {
            // Disable manual level
            if (manualLevelParent != null)
            {
                manualLevelParent.SetActive(false);
            }

            // Set which map to use
            currentMap = useTestMap ? testMap : levelMap;

            // Generate the procedural level
            GenerateProceduralLevel();
        }
        // If generateNewMap is false, manual level stays active and nothing happens
    }

    void GenerateProceduralLevel()
    {
        // Clear any existing procedural level
        ClearProceduralLevel();

        // Generate all four quadrants
        GenerateTopLeftQuadrant();
        GenerateTopRightQuadrant();
        GenerateBottomLeftQuadrant();
        GenerateBottomRightQuadrant();
    }

    void ClearProceduralLevel()
    {
        // Remove any existing generated tiles
        foreach (Transform child in proceduralLevelParent)
        {
            Destroy(child.gameObject);
        }
    }

    void GenerateTopLeftQuadrant()
    {
        int rows = currentMap.GetLength(0);
        int cols = currentMap.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int tileType = currentMap[row, col];
                if (tileType == 0) continue; // Skip empty spaces

                // Calculate position based on manuallevel coordinate system
                Vector3 position = new Vector3(0.5f + col, 9.5f - row, 0);

                // Calculate rotation based on surrounding tiles
                float rotation = CalculateTileRotation(row, col, tileType, false, false);

                // Create the tile
                CreateTile(tileType, position, rotation);
            }
        }
    }

    void GenerateTopRightQuadrant()
    {
        int rows = currentMap.GetLength(0);
        int cols = currentMap.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int tileType = currentMap[row, col];
                if (tileType == 0) continue;

                // Mirror horizontally (coordinates: 14.5 to 27.5)
                Vector3 position = new Vector3(27.5f - col, 9.5f - row, 0);

                // Calculate rotation with horizontal flip
                float rotation = CalculateTileRotation(row, col, tileType, true, false);

                CreateTile(tileType, position, rotation);
            }
        }
    }

    void GenerateBottomLeftQuadrant()
    {
        int rows = currentMap.GetLength(0);
        int cols = currentMap.GetLength(1);

        // Skip last row to avoid duplication (specs requirement)
        for (int row = 0; row < rows - 1; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int tileType = currentMap[row, col];
                if (tileType == 0) continue;

                // Mirror vertically
                Vector3 position = new Vector3(0.5f + col, -18.5f + row, 0);

                // Calculate rotation with vertical flip
                float rotation = CalculateTileRotation(row, col, tileType, false, true);

                CreateTile(tileType, position, rotation);
            }
        }
    }

    void GenerateBottomRightQuadrant()
    {
        int rows = currentMap.GetLength(0);
        int cols = currentMap.GetLength(1);

        // Skip last row to avoid duplication
        for (int row = 0; row < rows - 1; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int tileType = currentMap[row, col];
                if (tileType == 0) continue;

                // Mirror both horizontally and vertically
                Vector3 position = new Vector3(27.5f - col, -18.5f + row, 0);

                // Calculate rotation with both flips
                float rotation = CalculateTileRotation(row, col, tileType, true, true);

                CreateTile(tileType, position, rotation);
            }
        }
    }

    float CalculateTileRotation(int row, int col, int tileType, bool flipHorizontal, bool flipVertical)
    {
        // Top-left corner always has default rotation (specs requirement)
        if (row == 0 && col == 0 && !flipHorizontal && !flipVertical)
        {
            return 0f;
        }

        float rotation = 0f;

        // Get detailed info about surrounding pieces
        bool[] connections = GetConnectionDirections(row, col);
        // Index: 0=Up, 1=Right, 2=Down, 3=Left

        if (tileType == 1 || tileType == 3) // Corners
        {
            rotation = CalculateCornerRotationAdvanced(connections);
        }
        else if (tileType == 2 || tileType == 4 || tileType == 8) // Walls
        {
            rotation = CalculateWallRotationAdvanced(connections);
        }
        else if (tileType == 7) // T-junction
        {
            rotation = CalculateTJunctionRotationAdvanced(connections);
        }

        // Apply mirroring adjustments
        rotation = ApplyMirroringRotation(rotation, tileType, flipHorizontal, flipVertical);

        return rotation % 360f;
    }

    bool[] GetConnectionDirections(int row, int col)
    {
        bool[] connections = new bool[4]; // Up, Right, Down, Left

        // Check each direction for pieces that should connect
        connections[0] = ShouldConnectTo(row - 1, col); // Up
        connections[1] = ShouldConnectTo(row, col + 1); // Right  
        connections[2] = ShouldConnectTo(row + 1, col); // Down
        connections[3] = ShouldConnectTo(row, col - 1); // Left

        return connections;
    }

    bool ShouldConnectTo(int row, int col)
    {
        int tileType = GetTileTypeAt(row, col);
        // Only connect to actual wall/corner pieces, not pellets or empty
        return tileType == 1 || tileType == 2 || tileType == 3 || tileType == 4 || tileType == 7 || tileType == 8;
    }

    float CalculateCornerRotationAdvanced(bool[] connections)
    {
        // Corner should connect exactly 2 adjacent directions
        // connections[0]=Up, [1]=Right, [2]=Down, [3]=Left

        if (connections[2] && connections[1]) return 0f;   // Down + Right = 0° (top-left style)
        if (connections[2] && connections[3]) return 270f; // Down + Left = 270° (top-right style)  
        if (connections[0] && connections[3]) return 180f; // Up + Left = 180° (bottom-right style)
        if (connections[0] && connections[1]) return 90f;  // Up + Right = 90° (bottom-left style)

        return 0f; // Default fallback
    }

    float CalculateWallRotationAdvanced(bool[] connections)
    {
        // Wall should connect 2 opposite directions

        if ((connections[0] && connections[2]) || // Up-Down connection
            (connections[0] && !connections[1] && !connections[2] && !connections[3]) || // Only up
            (!connections[0] && !connections[1] && connections[2] && !connections[3])) // Only down
        {
            return 90f; // Vertical wall
        }

        if ((connections[1] && connections[3]) || // Left-Right connection  
            (connections[1] && !connections[0] && !connections[2] && !connections[3]) || // Only right
            (!connections[0] && !connections[1] && !connections[2] && connections[3])) // Only left
        {
            return 0f; // Horizontal wall
        }

        // Default: horizontal
        return 0f;
    }

    float CalculateTJunctionRotationAdvanced(bool[] connections)
    {
        // T-junction should connect 3 directions, opening toward the 4th

        // Count connections
        int connectionCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if (connections[i]) connectionCount++;
        }

        if (connectionCount == 3)
        {
            // Find the missing direction (where T opens)
            if (!connections[0]) return 0f;   // Opens Up (T) shape
            if (!connections[1]) return 90f;  // Opens Right (|--) shape  
            if (!connections[2]) return 180f; // Opens Down (Inverted T) shape
            if (!connections[3]) return 270f; // Opens Left (--|) shape
        }

        // Fallback for edge cases - try to make best guess
        if (connections[1] && connections[2] && connections[3]) return 0f;   // Missing up
        if (connections[0] && connections[2] && connections[3]) return 90f;  // Missing right
        if (connections[0] && connections[1] && connections[3]) return 180f; // Missing down  
        if (connections[0] && connections[1] && connections[2]) return 270f; // Missing left

        return 0f; // Default
    }

    float ApplyMirroringRotation(float originalRotation, int tileType, bool flipH, bool flipV)
    {
        float newRotation = originalRotation;

        if (tileType == 1 || tileType == 3) // Corners
        {
            if (flipH && flipV) // Both flips
            {
                // 0->180, 90->270, 180->0, 270->90
                newRotation = (originalRotation + 180f) % 360f;
            }
            else if (flipH) // Horizontal flip only
            {
                // 0->270, 270->0, 90->180, 180->90
                if (originalRotation == 0f) newRotation = 270f;
                else if (originalRotation == 90f) newRotation = 180f;
                else if (originalRotation == 180f) newRotation = 90f;
                else if (originalRotation == 270f) newRotation = 0f;
            }
            else if (flipV) // Vertical flip only
            {
                // 0->90, 90->0, 180->270, 270->180
                if (originalRotation == 0f) newRotation = 90f;
                else if (originalRotation == 90f) newRotation = 0f;
                else if (originalRotation == 180f) newRotation = 270f;
                else if (originalRotation == 270f) newRotation = 180f;
            }
        }
        else if (tileType == 7) // T-junction
        {
            if (flipH && flipV) // Both flips
            {
                // 0->180, 90->270, 180->0, 270->90
                newRotation = (originalRotation + 180f) % 360f;
            }
            else if (flipH) // Horizontal flip only
            {
                // 0->0, 180->180, 90->270, 270->90
                if (originalRotation == 90f) newRotation = 270f;
                else if (originalRotation == 270f) newRotation = 90f;
            }
            else if (flipV) // Vertical flip only
            {
                // 0->180, 180->0, 90->90, 270->270
                if (originalRotation == 0f) newRotation = 180f;
                else if (originalRotation == 180f) newRotation = 0f;
            }
        }
        // Walls stay the same (they work in both orientations after mirroring)

        return newRotation;
    }

    int GetTileTypeAt(int row, int col)
    {
        // Safe array access with bounds checking
        if (row < 0 || row >= currentMap.GetLength(0) || col < 0 || col >= currentMap.GetLength(1))
        {
            return 0; // Out of bounds = empty
        }
        return currentMap[row, col];
    }

    void CreateTile(int tileType, Vector3 position, float rotation)
    {
        // Make sure we have a prefab for this tile type
        if (tileType < tilePrefabs.Length && tilePrefabs[tileType] != null)
        {
            GameObject newTile = Instantiate(tilePrefabs[tileType], position, Quaternion.Euler(0, 0, rotation));
            newTile.transform.SetParent(proceduralLevelParent);

            // Name it (for easier debugging)
            newTile.name = $"Tile_{tileType}_R{position.y}_C{position.x}";
        }
    }

    // Public method to access the level map for movement validation
    public int[,] GetLevelMap()
    {
        // Return the appropriate map based on what's being used
        if (generateNewMap && currentMap != null)
        {
            return currentMap;
        }
        // Default to standard levelMap for manual level
        return levelMap;
    }
}