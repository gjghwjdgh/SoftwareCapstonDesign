using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PathVisualizer : MonoBehaviour
{
    [Header("핵심 설정")]
    public Transform startPoint;
    public List<Transform> targets = new List<Transform>();
    public Transform uiCanvas;

    [Header("프리팹")]
    public GameObject lineDrawer3DPrefab;
    public GameObject uiLinePrefab;

    // GameUIManager와의 통신을 위해 참조를 추가합니다.
    [Header("관리자 연결")]
    public GameUIManager gameUIManager;

    private List<PathDrawer> pathDrawers3D = new List<PathDrawer>();
    private List<UILineConnector> uiLines2D = new List<UILineConnector>();
    private bool is3DMode = true;
    public bool Is3DMode => is3DMode;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (gameUIManager == null) gameUIManager = FindObjectOfType<GameUIManager>();
    }

    public void GenerateAndShowAllPaths()
    {
        if (gameUIManager == null) gameUIManager = FindObjectOfType<GameUIManager>();

        foreach (var line in pathDrawers3D) if (line != null) Destroy(line.gameObject);
        foreach (var line in uiLines2D) if (line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();
        uiLines2D.Clear();

        if (targets == null) targets = new List<Transform>();

        for (int i = 0; i < targets.Count; i++)
        {
            if (startPoint == null || targets[i] == null) continue;

            PathDrawer drawer3D = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();
            UILineConnector uiLine = Instantiate(uiLinePrefab, uiCanvas).GetComponent<UILineConnector>();

            // AR에서는 곡선 경로 기능이 아직 복잡하므로, 우선 직선으로만 처리합니다.
            // 필요하다면 나중에 AR용 곡선 설정 로직을 추가할 수 있습니다.
            drawer3D.InitializeLine(startPoint, targets[i]);
            uiLine.InitializeLine(startPoint, targets[i], mainCamera);

            pathDrawers3D.Add(drawer3D);
            uiLines2D.Add(uiLine);
        }
        UpdateViewMode();
    }

    public void SwitchViewMode() { is3DMode = !is3DMode; UpdateViewMode(); }

    void UpdateViewMode()
    {
        foreach (var line in pathDrawers3D) if (line != null) line.gameObject.SetActive(is3DMode);
        foreach (var line in uiLines2D) if (line != null) line.gameObject.SetActive(!is3DMode);
    }

    // ★★★ 여기가 핵심 수정 부분 ★★★
    // IndexOutOfRangeException을 원천적으로 방지하는 더 안전한 코드로 변경
    public void HighlightPath(int targetIndex)
    {
        for (int i = 0; i < pathDrawers3D.Count; i++)
        {
            if (pathDrawers3D[i] != null)
            {
                pathDrawers3D[i].SetHighlight(i == targetIndex);
            }
        }
        for (int i = 0; i < uiLines2D.Count; i++)
        {
            if (uiLines2D[i] != null)
            {
                uiLines2D[i].SetHighlight(i == targetIndex);
            }
        }
    }
}