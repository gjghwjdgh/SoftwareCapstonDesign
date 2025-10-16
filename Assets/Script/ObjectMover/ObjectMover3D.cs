using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ObjectMover3D : MonoBehaviour
{
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public IEnumerator MoveAlongPath(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        float elapsedTime = 0f;
        Camera mainCamera = Camera.main;

        try
        {
            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                float curveProgress = speedCurve.Evaluate(progress);

                // 경로가 2개 미만의 점으로 이루어져 있다면(직선 또는 오류), 기존 방식으로 처리
                if (path.Count < 2)
                {
                    transform.position = path.Count > 0 ? path[0] : Vector3.zero;
                }
                else
                {
                    float pathProgress = curveProgress * (path.Count - 1);
                    int currentIndex = Mathf.FloorToInt(pathProgress);
                    int nextIndex = Mathf.Min(currentIndex + 1, path.Count - 1);
                    float segmentProgress = pathProgress - currentIndex;

                    transform.position = Vector3.Lerp(path[currentIndex], path[nextIndex], segmentProgress);
                }

                targetScreenPath.Add(mainCamera.WorldToScreenPoint(transform.position));
                targetTimestamps.Add(Time.time);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (path.Count > 0)
            {
                transform.position = path[path.Count - 1];
                targetScreenPath.Add(mainCamera.WorldToScreenPoint(transform.position));
                targetTimestamps.Add(Time.time);
            }

            onComplete?.Invoke(targetScreenPath, targetTimestamps);
            Destroy(gameObject);
        }
    }
}