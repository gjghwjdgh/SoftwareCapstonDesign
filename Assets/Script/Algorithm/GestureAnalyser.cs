using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 제스처 유사도를 분석하는 함수들을 모아놓은 static 클래스 (도구상자)
/// </summary>
public static class GestureAnalyser
{
    /// <summary>
    /// 프레셰 거리(Frechet Distance)를 계산하여 두 경로의 '모양' 유사도를 측정합니다.
    /// </summary>
    /// <returns>거리가 멀수록(점수가 높을수록) 모양이 다릅니다.</returns>
    public static float CalculateFrechetDistance(List<Vector2> pathA, List<Vector2> pathB)
    {
        if (pathA == null || pathB == null || pathA.Count == 0 || pathB.Count == 0) return float.MaxValue;

        float[,] dp = new float[pathA.Count, pathB.Count];

        for (int i = 0; i < pathA.Count; i++)
        {
            for (int j = 0; j < pathB.Count; j++)
            {
                float cost = Vector2.Distance(pathA[i], pathB[j]);
                if (i > 0 && j > 0)
                {
                    dp[i, j] = Mathf.Max(Mathf.Min(dp[i - 1, j], dp[i - 1, j - 1], dp[i, j - 1]), cost);
                }
                else if (i > 0)
                {
                    dp[i, j] = Mathf.Max(dp[i - 1, j], cost);
                }
                else if (j > 0)
                {
                    dp[i, j] = Mathf.Max(dp[i, j - 1], cost);
                }
                else
                {
                    dp[i, j] = cost;
                }
            }
        }
        return dp[pathA.Count - 1, pathB.Count - 1];
    }

    /// <summary>
    /// 두 경로의 속도 프로파일을 비교하여 '속도' 유사도를 측정합니다.
    /// </summary>
    /// <returns>유사도가 높을수록 1에 가깝고, 낮을수록 0에 가깝습니다.</returns>
    public static float CalculateVelocitySimilarity(List<Vector2> userPath, List<float> userTimes, List<Vector2> targetPath, List<float> targetTimes)
    {
        if (userPath.Count < 2 || targetPath.Count < 2) return 0;

        List<float> userSpeeds = GetSpeedProfile(userPath, userTimes);
        List<float> targetSpeeds = GetSpeedProfile(targetPath, targetTimes);

        // 타겟의 평균 속도를 계산합니다. (0으로 나누는 것을 방지)
        float targetAverageSpeed = targetSpeeds.Count > 0 ? targetSpeeds.Average() : 0;
        if (targetAverageSpeed < 1f) targetAverageSpeed = 1f;

        List<float> resampledUserSpeeds = Resample(userSpeeds, 100);
        List<float> resampledTargetSpeeds = Resample(targetSpeeds, 100);

        float diffSum = 0;
        for (int i = 0; i < 100; i++)
        {
            diffSum += Mathf.Abs(resampledUserSpeeds[i] - resampledTargetSpeeds[i]);
        }
        float avgDiff = diffSum / 100;

        // 속도 차이를 타겟의 평균 속도로 나누어 '정규화'합니다.
        float normalizedDifference = avgDiff / targetAverageSpeed;

        // 정규화된 차이 값을 사용하므로 민감도 계수(0.01f)를 제거하여 더 직관적으로 만듭니다.
        return Mathf.Exp(-normalizedDifference);
    }

    // 경로와 시간 데이터로부터 각 구간의 속도 리스트를 생성합니다.
    private static List<float> GetSpeedProfile(List<Vector2> path, List<float> times)
    {
        List<float> speeds = new List<float>();
        if (path.Count < 2) return speeds;
        
        for (int i = 1; i < path.Count; i++)
        {
            float distance = Vector2.Distance(path[i], path[i - 1]);
            float time = times[i] - times[i - 1];
            if (time > 0.0001f)
            {
                speeds.Add(distance / time);
            }
            else
            {
                speeds.Add(0);
            }
        }
        return speeds;
    }

    // 데이터 리스트를 원하는 크기로 리샘플링합니다 (선형 보간).
    private static List<float> Resample(List<float> data, int newSize)
    {
        if (data == null || data.Count < 2) return Enumerable.Repeat(0f, newSize).ToList();

        List<float> resampled = new List<float>(newSize);
        for (int i = 0; i < newSize; i++)
        {
            float t = (float)i / (newSize - 1);
            float originalIndex = t * (data.Count - 1);
            int i0 = (int)originalIndex;
            int i1 = Mathf.Min(i0 + 1, data.Count - 1);
            resampled.Add(Mathf.Lerp(data[i0], data[i1], originalIndex - i0));
        }
        return resampled;
    }
}