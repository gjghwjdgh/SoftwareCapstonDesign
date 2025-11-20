using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ObjectMover3D : MonoBehaviour
{
    // 페이즈 위치를 정확히 반영하기 위해 Linear 사용 (0.5는 정확히 50% 지점)
    public AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);
    private bool shouldLoop = true;

    public void StopMovement() { shouldLoop = false; }

    // ★★★ 외부에서 강제로 위치를 잡는 함수 (생성 즉시 호출용) ★★★
    public void ForceSetPosition(List<Vector3> path, float rawProgress)
    {
        UpdatePosition(path, rawProgress);
    }

    public IEnumerator MoveAlongPathWithPhase(List<Vector3> path, float duration, float startPhase, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        Camera mainCamera = Camera.main;
        bool hasCompletedOnce = false;

        float currentProgress = startPhase;

        // 여기서도 한번 더 확실하게 위치 세팅
        UpdatePosition(path, currentProgress);

        while (shouldLoop)
        {
            currentProgress += Time.deltaTime / duration;

            if (currentProgress >= 1.0f)
            {
                currentProgress %= 1.0f;

                if (!hasCompletedOnce)
                {
                    if (path.Any())
                    {
                        targetScreenPath.Add(mainCamera.WorldToScreenPoint(path.Last()));
                        targetTimestamps.Add(Time.time);
                    }
                    onComplete?.Invoke(targetScreenPath, targetTimestamps);
                    hasCompletedOnce = true;
                }
            }

            UpdatePosition(path, currentProgress);

            if (!hasCompletedOnce)
            {
                targetScreenPath.Add(mainCamera.WorldToScreenPoint(transform.position));
                targetTimestamps.Add(Time.time);
            }

            yield return null;
        }
        Destroy(gameObject);
    }

    private void UpdatePosition(List<Vector3> path, float rawProgress)
    {
        if (path == null || path.Count == 0) return;

        float curveProgress = speedCurve.Evaluate(rawProgress);
        float pathVal = curveProgress * (path.Count - 1);
        int idx = Mathf.FloorToInt(pathVal);
        int nextIdx = Mathf.Min(idx + 1, path.Count - 1);
        float t = pathVal - idx;

        if (idx < path.Count && nextIdx < path.Count)
        {
            transform.position = Vector3.Lerp(path[idx], path[nextIdx], t);
        }
    }
}