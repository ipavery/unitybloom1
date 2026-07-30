using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class ResponsiveGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int targetColumns = 3;
    
    [Tooltip("Scales the tiles down from their maximum allowed size. 1 = 100%, 0.5 = 50%.")]
    [Range(0.1f, 1f)]
    public float tileSizeMultiplier = 1f;
    
    private GridLayoutGroup gridLayout;
    private RectTransform rectTransform;
    
    public int ItemsPerPage { get; private set; } = 1;

    // Changed to Awake so references are guaranteed to exist 
    // before another script calls RecalculateGrid() on the same frame.
    void Awake()
    {
        gridLayout = GetComponent<GridLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void RecalculateGrid()
    {
        if (gridLayout == null || rectTransform == null) Awake(); // Failsafe

        // 1. FORCE Unity to calculate the UI dimensions immediately BEFORE we do math.
        // This solves the "first load" incorrect sizing bug.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = targetColumns;

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        // 2. Calculate maximum possible size based on target columns
        float horizontalPadding = gridLayout.padding.left + gridLayout.padding.right;
        float horizontalSpacing = gridLayout.spacing.x * (targetColumns - 1);
        float maxCellWidth = (width - horizontalPadding - horizontalSpacing) / targetColumns;
        
        // 3. Calculate maximum possible size based on available height
        float verticalPadding = gridLayout.padding.top + gridLayout.padding.bottom;
        float availableHeight = height - verticalPadding;
        
        // 4. Calculate size and apply it. 
        // We use Mathf.Floor to ensure we drop any tiny decimals (like .0001) 
        // that cause the GridLayoutGroup to accidentally wrap to the next line.
        float squareSize = Mathf.Floor(Mathf.Min(maxCellWidth, availableHeight) * tileSizeMultiplier);
        gridLayout.cellSize = new Vector2(squareSize, squareSize);

        // 5. Calculate rows cleanly without the epsilon hack
        int rows = Mathf.FloorToInt((availableHeight + gridLayout.spacing.y) / (squareSize + gridLayout.spacing.y));
        
        // Failsafe: Ensure at least 1 row is counted
        rows = Mathf.Max(1, rows);
        
        ItemsPerPage = targetColumns * rows;
    }
}