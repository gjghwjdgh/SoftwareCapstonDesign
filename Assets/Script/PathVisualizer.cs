using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    [Header("핵심 설정")]
    public Transform startPoint;
    public List<Transform> targets;
    public Transform uiCanvas;

    [Header("프리팹")]
    public GameObject lineDrawer3DPrefab;
    public GameObject uiLinePrefab;

    private List<PathDrawer> pathDrawers3D = new List<PathDrawer>();
    private List<UILineConnector> uiLines2D = new List<UILineConnector>();
    private bool is3DMode = true;
    public bool Is3DMode => is3DMode;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        // ✨ 이 코드를 추가하면 시작하자마자 선이 생성됩니다.
        GenerateAndShowAllPaths();
    }

    public void GenerateAndShowAllPaths()
    {
        foreach (var line in pathDrawers3D) if (line != null) Destroy(line.gameObject);
        foreach (var line in uiLines2D) if (line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();
        uiLines2D.Clear();

        for (int i = 0; i < targets.Count; i++)
        {
            PathDrawer drawer3D = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();
            drawer3D.Initialize(startPoint, targets[i]);
            pathDrawers3D.Add(drawer3D);

            UILineConnector uiLine = Instantiate(uiLinePrefab, uiCanvas).GetComponent<UILineConnector>();
            uiLine.Initialize(startPoint, targets[i], mainCamera);
            uiLines2D.Add(uiLine);
        }
        UpdateViewMode();
    }

    public Transform GetTargetTransform(int index) { return (index < 0 || index >= targets.Count) ? null : targets[index]; }
    public UILineConnector GetUILine(int index) { return (index < 0 || index >= uiLines2D.Count) ? null : uiLines2D[index]; }

    public void SwitchViewMode() { is3DMode = !is3DMode; UpdateViewMode(); }
    void UpdateViewMode()
    {
        foreach (var line in pathDrawers3D) line.gameObject.SetActive(is3DMode);
        foreach (var line in uiLines2D) line.gameObject.SetActive(!is3DMode);
    }

    public void HighlightPath(int targetIndex)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            bool isHighlighted = (i == targetIndex);
            if (i < pathDrawers3D.Count) pathDrawers3D[i].SetHighlight(isHighlighted);
            if (i < uiLines2D.Count) uiLines2D[i].SetHighlight(isHighlighted);
        }
    }
}