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
    private Camera mainCamera;
    
    private bool is3DMode = true;
    // --- ▼ 여기가 추가된 부분입니다 ▼ ---
    // 외부에서 현재 모드(is3DMode)가 무엇인지 '읽을 수만' 있도록 공개하는 프로퍼티(Property)입니다.
    public bool Is3DMode => is3DMode;
    // --- ▲ 여기가 추가된 부분입니다 ▲ ---

    void Start()
    {
        mainCamera = Camera.main;
    }

    public void ShowAllPaths()
    {
        foreach (var line in pathDrawers3D) if(line != null) Destroy(line.gameObject);
        foreach (var line in uiLines2D) if(line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();
        uiLines2D.Clear();

        for (int i = 0; i < targets.Count; i++)
        {
            GameObject drawerInstance3D = Instantiate(lineDrawer3DPrefab, transform);
            PathDrawer drawer3D = drawerInstance3D.GetComponent<PathDrawer>();
            drawer3D.Initialize(startPoint, targets[i]);
            pathDrawers3D.Add(drawer3D);

            GameObject uiLineInstance = Instantiate(uiLinePrefab, uiCanvas);
            UILineConnector uiLine = uiLineInstance.GetComponent<UILineConnector>();
            uiLine.Initialize(startPoint, targets[i], mainCamera);
            uiLines2D.Add(uiLine);
        }
        UpdateViewMode();
    }
    
    public Transform GetTargetTransform(int index)
    {
        if (index < 0 || index >= targets.Count) return null;
        return targets[index];
    }
    
    public void SwitchViewMode()
    {
        is3DMode = !is3DMode;
        UpdateViewMode();
    }

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
            if(i < pathDrawers3D.Count && pathDrawers3D[i] != null) pathDrawers3D[i].SetHighlight(isHighlighted);
            if(i < uiLines2D.Count && uiLines2D[i] != null) uiLines2D[i].SetHighlight(isHighlighted);
        }
    }

    public UILineConnector GetUILine(int index)
    {
        if (index < 0 || index >= uiLines2D.Count) return null;
        return uiLines2D[index];
    }
}