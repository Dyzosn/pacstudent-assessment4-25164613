using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

public class BorderAnimation : MonoBehaviour
{
    // Prefab for the moving dot
    public GameObject dotPrefab;

    // Number of dots around the border
    public int dotCount = 40;

    // Movement speed (pixels per second)
    public float moveSpeed = 100f;

    // Store all active dots
    private RectTransform[] dots;
    private RectTransform canvasRect;

    void Start()
    {
        // Get canvas dimensions for calculations
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();

        // Spawn all dots
        SpawnDots();
    }

    void SpawnDots()
    {
        dots = new RectTransform[dotCount];

        // Create each dot around the perimeter
        for (int i = 0; i < dotCount; i++)
        {
            // Instantiate dot from prefab
            GameObject newDot = Instantiate(dotPrefab, transform);
            dots[i] = newDot.GetComponent<RectTransform>();

            // Calculate initial position based on dot index
            float progressAroundBorder = (float)i / dotCount;
            PositionDotOnBorder(dots[i], progressAroundBorder);

            // Store progress value in a helper component
            DotProgress progress = newDot.AddComponent<DotProgress>();
            progress.progress = progressAroundBorder;
        }
    }

    void Update()
    {
        // Move all dots each frame
        foreach (RectTransform dot in dots)
        {
            MoveDotAlongBorder(dot);
        }
    }

    void MoveDotAlongBorder(RectTransform dot)
    {
        // Get stored progress value
        DotProgress progressData = dot.GetComponent<DotProgress>();

        // Calculate how much to move this frame
        float borderLength = CalculateBorderLength();
        float progressIncrement = (moveSpeed * Time.deltaTime) / borderLength;

        // Update progress (loop back to 0 after reaching 1)
        progressData.progress += progressIncrement;
        if (progressData.progress >= 1f)
        {
            progressData.progress -= 1f;
        }

        // Update dot position
        PositionDotOnBorder(dot, progressData.progress);
    }

    void PositionDotOnBorder(RectTransform dot, float progress)
    {
        // Get border dimensions (with padding accounted for)
        float width = canvasRect.rect.width - 40f;  // 20px padding each side
        float height = canvasRect.rect.height - 40f;

        // Calculate total perimeter
        float perimeter = 2f * (width + height);

        // Convert progress to distance along perimeter
        float distanceAlongBorder = progress * perimeter;

        // Calculate position based on which edge we're on
        Vector2 position;

        if (distanceAlongBorder < width)
        {
            // Top edge (left to right)
            position = new Vector2(-width / 2f + distanceAlongBorder, height / 2f);
        }
        else if (distanceAlongBorder < width + height)
        {
            // Right edge (top to bottom)
            float d = distanceAlongBorder - width;
            position = new Vector2(width / 2f, height / 2f - d);
        }
        else if (distanceAlongBorder < 2f * width + height)
        {
            // Bottom edge (right to left)
            float d = distanceAlongBorder - width - height;
            position = new Vector2(width / 2f - d, -height / 2f);
        }
        else
        {
            // Left edge (bottom to top)
            float d = distanceAlongBorder - 2f * width - height;
            position = new Vector2(-width / 2f, -height / 2f + d);
        }

        dot.anchoredPosition = position;
    }

    float CalculateBorderLength()
    {
        float width = canvasRect.rect.width - 40f;
        float height = canvasRect.rect.height - 40f;
        return 2f * (width + height);
    }
}

// Helper class to store each dot's progress value
public class DotProgress : MonoBehaviour
{
    public float progress;
}