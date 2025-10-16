using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PathVisualizer : MonoBehaviour
{
    [Header("핵심 설정")]
    public Transform startPoint;
    public List<Transform> targets;
    public Transform uiCanvas;

    [Header("프리팹")]
    public GameObject lineDrawer3DPrefab;
    public GameObject uiLinePrefab;

    private GameUIManager gameUIManager;
    private List<PathDrawer> pathDrawers3D = new List<PathDrawer>();
    private List<UILineConnector> uiLines2D = new List<UILineConnector>();
    private bool is3DMode = true;
    public bool Is3DMode => is3DMode;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        gameUIManager = FindAnyObjectByType<GameUIManager>();
        GenerateAndShowAllPaths();
    }

    public void GenerateAndShowAllPaths()
    {
        if (gameUIManager == null) gameUIManager = FindAnyObjectByType<GameUIManager>();

        foreach (var line in pathDrawers3D) if (line != null) Destroy(line.gameObject);
        foreach (var line in uiLines2D) if (line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();
        uiLines2D.Clear();

        for (int i = 0; i < targets.Count; i++)
        {
            PathDrawer drawer3D = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();
            UILineConnector uiLine = Instantiate(uiLinePrefab, uiCanvas).GetComponent<UILineConnector>();

            bool isCurve = (i == 0 && gameUIManager.IsTarget1CurveMode && gameUIManager.target1_ControlPoint1 != null && gameUIManager.target1_ControlPoint2 != null);

            if (isCurve)
            {
                var pathPoints = PathUtilities.GenerateBezierCurvePath(
                    startPoint.position,
                    gameUIManager.target1_ControlPoint1.position,
                    gameUIManager.target1_ControlPoint2.position,
                    targets[i].position,
                    gameUIManager.curveSegmentCount
                );
                drawer3D.InitializeCurve(pathPoints);
                uiLine.InitializeCurve(pathPoints, mainCamera);
            }
            else
            {
                drawer3D.InitializeLine(startPoint, targets[i]);
                uiLine.InitializeLine(startPoint, targets[i], mainCamera);
            }
            pathDrawers3D.Add(drawer3D);
            uiLines2D.Add(uiLine);
        }
        UpdateViewMode();
    }

    public Transform GetTargetTransform(int index) { return (index < 0 || index >= targets.Count) ? null : targets[index]; }
    public UILineConnector GetUILine(int index) { return (index < 0 || index >= uiLines2D.Count) ? null : uiLines2D[index]; }
    public void SwitchViewMode() { is3DMode = !is3DMode; UpdateViewMode(); }
    void UpdateViewMode()
    {
        foreach (var line in pathDrawers3D) if (line != null) line.gameObject.SetActive(is3DMode);
        foreach (var line in uiLines2D) if (line != null) line.gameObject.SetActive(!is3DMode);
    }
    public void HighlightPath(int targetIndex)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            bool isHighlighted = (i == targetIndex);
            if (i < pathDrawers3D.Count && pathDrawers3D[i] != null) pathDrawers3D[i].SetHighlight(isHighlighted);
            if (i < uiLines2D.Count && uiLines2D[i] != null) uiLines2D[i].SetHighlight(isHighlighted);
        }
    }
}