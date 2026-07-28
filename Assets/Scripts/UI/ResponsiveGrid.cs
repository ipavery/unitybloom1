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

    void Start()
    {
        gridLayout = GetComponent<GridLayoutGroup>();
        // print(gridLayout);
        rectTransform = GetComponent<RectTransform>();
    }

    public void RecalculateGrid()
    {
        // 1. FORCE the grid to respect the exact number of columns, even if tiles get smaller
        //Debug.Log(gridLayout);
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
        
        // 4. Calculate size and apply it
        float squareSize = Mathf.Min(maxCellWidth, availableHeight) * tileSizeMultiplier;
        gridLayout.cellSize = new Vector2(squareSize, squareSize);

        // 5. Calculate rows, adding a small 0.1f epsilon to prevent precision rounding errors from dropping a row
        int rows = Mathf.FloorToInt((availableHeight + gridLayout.spacing.y + 0.1f) / (squareSize + gridLayout.spacing.y));
        
        // Failsafe: Ensure at least 1 row is counted
        rows = Mathf.Max(1, rows);
        
        ItemsPerPage = targetColumns * rows;
    }

    // void OnRectTransformDimensionsChange()
    // {
    //     RecalculateGrid();
    // }
}