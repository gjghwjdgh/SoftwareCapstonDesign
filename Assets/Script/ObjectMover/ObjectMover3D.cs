using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ObjectMover3D : MonoBehaviour
{
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private bool shouldLoop = true; // 반복 여부를 제어하는 플래그

    // GameUIManager가 "정지!" 명령을 내릴 때 호출할 함수
    public void StopMovement()
    {
        shouldLoop = false;
    }

    public IEnumerator MoveAlongPath(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        Camera mainCamera = Camera.main;

        // onComplete 콜백은 '분석 데이터 수집'을 위해 딱 한 번만 실행합니다.
        bool hasCompletedOnce = false;

        // shouldLoop 플래그가 true인 동안 무한히 반복합니다.
        while (shouldLoop)
        {
            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                if (!shouldLoop) break; // 외부에서 정지 명령이 들어오면 즉시 중단

                float progress = elapsedTime / duration;
                float curveProgress = speedCurve.Evaluate(progress);

                float pathProgress = curveProgress * (path.Count - 1);
                int currentIndex = Mathf.FloorToInt(pathProgress);
                int nextIndex = Mathf.Min(currentIndex + 1, path.Count - 1);
                float segmentProgress = pathProgress - currentIndex;

                if (currentIndex < path.Count && nextIndex < path.Count)
                {
                    transform.position = Vector3.Lerp(path[currentIndex], path[nextIndex], segmentProgress);
                }

                // 분석 데이터는 딱 한 번만 기록합니다.
                if (!hasCompletedOnce)
                {
                    targetScreenPath.Add(mainCamera.WorldToScreenPoint(transform.position));
                    targetTimestamps.Add(Time.time);
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // 분석 데이터 기록 및 콜백 호출 (최초 한 번만)
            if (!hasCompletedOnce && path.Any())
            {
                transform.position = path.Last();
                targetScreenPath.Add(mainCamera.WorldToScreenPoint(transform.position));
                targetTimestamps.Add(Time.time);
                onComplete?.Invoke(targetScreenPath, targetTimestamps);
                hasCompletedOnce = true;
            }
        }

        // 루프가 끝나면(정지 명령을 받으면) 스스로를 파괴합니다.
        Destroy(gameObject);
    }
}