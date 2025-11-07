using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;

public class PathVisualizer : MonoBehaviour
{
    [Header("핵심 설정")]
    public Transform startPoint;
    public List<Transform> targets = new List<Transform>();

    [Header("프리팹")]
    public GameObject lineDrawer3DPrefab;

    [Header("관리자 연결")]
    public GameUIManager gameUIManager; // 곡률 강도 등 설정을 참조하기 위해 필수

    private List<PathDrawer> pathDrawers3D = new List<PathDrawer>();

    void Start()
    {
        if (gameUIManager == null) gameUIManager = FindObjectOfType<GameUIManager>();
    }

    public void GenerateAndShowAllPaths()
    {
        foreach (var line in pathDrawers3D) if (line != null) Destroy(line.gameObject);
        pathDrawers3D.Clear();

        if (startPoint == null || targets == null || targets.Count == 0 || gameUIManager == null)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue;

            PathDrawer drawer3D = Instantiate(lineDrawer3DPrefab, transform).GetComponent<PathDrawer>();

            // ★★★ 여기가 핵심 수정 부분 ★★★
            // 부분 경로가 아닌, '전체 곡선 경로'를 계산합니다.
            Vector3 p0 = startPoint.position;
            Vector3 p2 = targets[i].position;

            Vector3 midPoint = (p0 + p2) / 2f;
            Vector3 direction = (p2 - p0).normalized;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            float curveDirection = (i % 2 == 0) ? 1f : -1f;
            float curveMagnitude = (i / 2 + 1) * gameUIManager.curvatureStrength;
            Vector3 controlPoint = midPoint + (perpendicular * curveDirection * curveMagnitude);

            // PathUtilities를 사용하여 '전체 곡선 경로'를 생성합니다.
            List<Vector3> fullPath = PathUtilities.GenerateQuadraticBezierCurvePath(p0, controlPoint, p2, 50);

            // 계산된 '전체 경로'를 화면에 그리도록 명령합니다.
            drawer3D.InitializeCurve(fullPath);

            pathDrawers3D.Add(drawer3D);
        }
    }

    public void HighlightPath(int targetIndex)
    {
        for (int i = 0; i < pathDrawers3D.Count; i++)
        {
            if (pathDrawers3D[i] != null)
                pathDrawers3D[i].SetHighlight(i == targetIndex);
        }
    }
}