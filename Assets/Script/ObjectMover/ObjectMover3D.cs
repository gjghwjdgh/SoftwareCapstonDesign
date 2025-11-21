using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ObjectMover3D : MonoBehaviour
{
    public AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);
    private bool shouldLoop = true;

    public void StopMovement() { shouldLoop = false; }

    // ★★★ PursuitMover가 호출하는 초기화 함수 추가 ★★★
    // 코루틴 시작 전에 즉시 위치를 잡기 위해 외부에서 호출합니다.
    public void ForceSetPosition(List<Vector3> path, float startPhase)
    {
        // 시간 비율 0.0일 때의 위치(즉, StartPhase 위치)로 이동
        UpdatePositionMapped(path, 0f, startPhase);
    }

    public IEnumerator MoveAlongPath(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        return MoveAlongPathWithPhase(path, duration, 0f, onComplete);
    }

    public IEnumerator MoveAlongPathWithPhase(List<Vector3> path, float duration, float startPhase, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        Camera mainCamera = Camera.main;
        bool hasCompletedOnce = false;

        float timer = 0f;

        // 코루틴 시작 시점에도 위치를 확실히 잡음
        UpdatePositionMapped(path, 0f, startPhase);

        while (shouldLoop)
        {
            timer += Time.deltaTime;

            if (timer >= duration)
            {
                timer %= duration;

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

            float timeRatio = timer / duration;

            // 0~1 시간을 StartPhase~1 구간으로 맵핑하여 이동
            UpdatePositionMapped(path, timeRatio, startPhase);

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

    private void UpdatePositionMapped(List<Vector3> path, float timeRatio, float startPhase)
    {
        // timeRatio(0~1)를 startPhase~1.0 사이의 값으로 변환
        float mappedProgress = Mathf.Lerp(startPhase, 1.0f, timeRatio);
        UpdatePosition(path, mappedProgress);
    }
}