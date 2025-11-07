using UnityEngine;
using System.Collections.Generic;

public static class PathUtilities
{
    // 2차 베지어 곡선(Quadratic Bezier Curve) 위의 한 점을 계산하는 함수
    // p0: 시작점, p1: 조절점, p2: 끝점, t: 0.0 ~ 1.0 사이의 비율
    private static Vector3 GetQuadraticBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * p0) +
               (2f * oneMinusT * t * p1) +
               (t * t * p2);
    }

    // 2차 베지어 곡선 경로를 생성하는 함수
    public static List<Vector3> GenerateQuadraticBezierCurvePath(Vector3 startPoint, Vector3 controlPoint, Vector3 endPoint, int segmentCount)
    {
        List<Vector3> pathPoints = new List<Vector3>();
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            pathPoints.Add(GetQuadraticBezierPoint(startPoint, controlPoint, endPoint, t));
        }
        return pathPoints;
    }
}