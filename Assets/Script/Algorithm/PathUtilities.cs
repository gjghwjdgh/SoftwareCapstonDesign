using UnityEngine;
using System.Collections.Generic;

public static class PathUtilities
{
    // 3차 베지어 곡선(점이 4개)의 특정 지점 위치를 계산합니다.
    private static Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * oneMinusT * p0) +
               (3f * oneMinusT * oneMinusT * t * p1) +
               (3f * oneMinusT * t * t * p2) +
               (t * t * t * p3);
    }

    // 4개의 제어점(시작, 제어1, 제어2, 끝)을 받아 부드러운 곡선 경로를 점들의 리스트로 반환합니다.
    public static List<Vector3> GenerateBezierCurvePath(Vector3 startPoint, Vector3 controlPoint1, Vector3 controlPoint2, Vector3 endPoint, int segmentCount)
    {
        List<Vector3> pathPoints = new List<Vector3>();
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            pathPoints.Add(GetBezierPoint(startPoint, controlPoint1, controlPoint2, endPoint, t));
        }
        return pathPoints;
    }
}