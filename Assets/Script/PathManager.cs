using UnityEngine;
using System.Collections.Generic;

// BezierPath 구조체는 파일 상단에 그대로 두거나, 별도 파일로 분리해도 됩니다.
public struct BezierPath
{
    public Vector3 p0, p1, p2, p3;
}

public class PathManager : MonoBehaviour
{
    [Header("경로 설정")]
    public Transform startPoint;
    public List<Transform> targets;
    [Tooltip("곡선이 옆으로 퍼지는 정도를 조절합니다.")]
    public float spreadFactor = 2.5f;
    [Tooltip("곡선의 부드러움을 결정합니다. 높을수록 부드러운 경로가 생성됩니다.")]
    [Range(20, 200)]
    public int pathResolution = 100;

    // private List<BezierPath> calculatedPaths; // --- 삭제: 더 이상 미리 계산하지 않음 ---
    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
        // --- 삭제: Awake에서 PreCalculateAllPaths() 호출을 제거합니다. ---
    }

    /// <summary>
    /// [수정됨] 지정된 인덱스의 경로를 '실시간으로 계산'하여 부드러운 점들의 리스트로 반환합니다.
    /// </summary>
    public List<Vector3> GetPathPoints(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= targets.Count) return null;

        // --- 경로 계산 로직을 이 함수 안으로 이동 ---
        BezierPath path = CalculatePathForTarget(targetIndex);

        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i <= pathResolution; i++)
        {
            float t = i / (float)pathResolution;
            points.Add(CalculateCubicBezierPoint(t, path.p0, path.p1, path.p2, path.p3));
        }
        return points;
    }

    /// <summary>
    /// [새로 추가됨] 특정 타겟에 대한 베지어 경로 구조체를 실시간으로 계산합니다.
    /// </summary>
    private BezierPath CalculatePathForTarget(int targetIndex)
    {
        Vector3 p0 = startPoint.position;
        Vector3 p3 = targets[targetIndex].position;

        Vector3 directionVector = p3 - p0;

        // 현재 카메라의 'up' 벡터를 사용하여 사용자가 보는 방향 기준으로 sideVector를 계산
        Vector3 sideVector = Vector3.Cross(directionVector, mainCamera.transform.up).normalized;

        // 만약 sideVector가 (0,0,0)에 가까워지면 (카메라가 경로와 정면으로 마주볼 때),
        // 안정적인 월드 'up' 벡터를 대신 사용합니다. (안전장치)
        if (sideVector.sqrMagnitude < 0.01f)
        {
            sideVector = Vector3.Cross(directionVector, Vector3.up).normalized;
        }

        float distributionOffset = (targetIndex - (targets.Count - 1) / 2.0f) * spreadFactor;
        Vector3 offset = sideVector * distributionOffset;

        Vector3 p1 = p0 + (directionVector * 0.25f) + offset;
        Vector3 p2 = p3 - (directionVector * 0.25f) + offset;

        return new BezierPath { p0 = p0, p1 = p1, p2 = p2, p3 = p3 };
    }


    // 3차 베지어 곡선 계산 함수 (변경 없음)
    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;

        return p;
    }
}