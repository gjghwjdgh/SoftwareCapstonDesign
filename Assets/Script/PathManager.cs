using UnityEngine;
using System.Collections.Generic;

// BezierPath ����ü�� ���� ��ܿ� �״�� �ΰų�, ���� ���Ϸ� �и��ص� �˴ϴ�.
public struct BezierPath
{
    public Vector3 p0, p1, p2, p3;
}

public class PathManager : MonoBehaviour
{
    [Header("��� ����")]
    public Transform startPoint;
    public List<Transform> targets;
    [Tooltip("��� ������ ������ ������ �����մϴ�.")]
    public float spreadFactor = 2.5f;
    [Tooltip("��� �ε巯���� �����մϴ�. �������� �ε巯�� ��ΰ� �����˴ϴ�.")]
    [Range(20, 200)]
    public int pathResolution = 100;

    // private List<BezierPath> calculatedPaths; // --- ����: �� �̻� �̸� ������� ���� ---
    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
        // --- ����: Awake���� PreCalculateAllPaths() ȣ���� �����մϴ�. ---
    }

    /// <summary>
    /// [������] ������ �ε����� ��θ� '�ǽð����� ���'�Ͽ� �ε巯�� ������ ����Ʈ�� ��ȯ�մϴ�.
    /// </summary>
    public List<Vector3> GetPathPoints(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= targets.Count) return null;

        // --- ��� ��� ������ �� �Լ� ������ �̵� ---
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
    /// [���� �߰���] Ư�� Ÿ�ٿ� ���� ������ ��� ����ü�� �ǽð����� ����մϴ�.
    /// </summary>
    private BezierPath CalculatePathForTarget(int targetIndex)
    {
        Vector3 p0 = startPoint.position;
        Vector3 p3 = targets[targetIndex].position;

        Vector3 directionVector = p3 - p0;

        // ���� ī�޶��� 'up' ���͸� ����Ͽ� ����ڰ� ���� ���� �������� sideVector�� ���
        Vector3 sideVector = Vector3.Cross(directionVector, mainCamera.transform.up).normalized;

        // ���� sideVector�� (0,0,0)�� ��������� (ī�޶� ��ο� �������� ���ֺ� ��),
        // �������� ���� 'up' ���͸� ��� ����մϴ�. (������ġ)
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


    // 3�� ������ � ��� �Լ� (���� ����)
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