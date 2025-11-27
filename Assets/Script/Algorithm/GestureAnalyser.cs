using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class GestureAnalyser
{
    // 프레셰 거리 계산 (변경 없음)
    public static float CalculateFrechetDistance(List<Vector2> pathA, List<Vector2> pathB)
    {
        if (pathA == null || pathB == null || pathA.Count == 0 || pathB.Count == 0) return float.MaxValue;
        float[,] dp = new float[pathA.Count, pathB.Count];
        for (int i = 0; i < pathA.Count; i++)
        {
            for (int j = 0; j < pathB.Count; j++)
            {
                float cost = Vector2.Distance(pathA[i], pathB[j]);
                if (i > 0 && j > 0) dp[i, j] = Mathf.Max(Mathf.Min(dp[i - 1, j], dp[i - 1, j - 1], dp[i, j - 1]), cost);
                else if (i > 0) dp[i, j] = Mathf.Max(dp[i - 1, j], cost);
                else if (j > 0) dp[i, j] = Mathf.Max(dp[i, j - 1], cost);
                else dp[i, j] = cost;
            }
        }
        return dp[pathA.Count - 1, pathB.Count - 1];
    }

    // ✨ 1. 설계 문서 기반 속도 유사도 계산 로직
    public static float CalculateVelocitySimilarity(float userAverageSpeed, float correctedTargetAverageSpeed)
    {
        if (correctedTargetAverageSpeed < 1.0f)
        {
            return (userAverageSpeed < 1.0f) ? 1.0f : 0.0f;
        }

        float speedDifference = Mathf.Abs(userAverageSpeed - correctedTargetAverageSpeed);
        float normalizedDifference = speedDifference / correctedTargetAverageSpeed;

        return Mathf.Exp(-1.5f * normalizedDifference);
    }

    // ✨ 2. 오류의 원인이었던, 빠져있던 평균 속도 계산 함수
    public static float GetAverageSpeed(List<Vector2> path, List<float> times)
    {
        if (path.Count < 2) return 0;

        List<float> speeds = new List<float>();
        for (int i = 1; i < path.Count; i++)
        {
            float distance = Vector2.Distance(path[i], path[i - 1]);
            float time = times[i] - times[i - 1];
            if (time > 0.0001f) speeds.Add(distance / time);
            else speeds.Add(0);
        }
        return speeds.DefaultIfEmpty(0).Average();
    }
}