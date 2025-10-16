using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Image))]
public class ObjectMover2D : MonoBehaviour
{
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    //  --- 핵심 변경: 인자로 2D 스크린 경로가 아닌 3D 월드 경로를 받습니다 ---
    public IEnumerator MoveOnScreen(List<Vector3> worldPath, float duration, System.Action<List<Vector2>, List<float>> onComplete)
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

                // --- 핵심 변경: 3D 경로를 기준으로 현재 월드 위치를 계산 ---
                Vector3 currentWorldPos;
                if (worldPath.Count < 2)
                {
                    currentWorldPos = worldPath.Count > 0 ? worldPath[0] : Vector3.zero;
                }
                else
                {
                    float pathProgress = curveProgress * (worldPath.Count - 1);
                    int currentIndex = Mathf.FloorToInt(pathProgress);
                    int nextIndex = Mathf.Min(currentIndex + 1, worldPath.Count - 1);
                    float segmentProgress = pathProgress - currentIndex;
                    currentWorldPos = Vector3.Lerp(worldPath[currentIndex], worldPath[nextIndex], segmentProgress);
                }

                // --- 핵심 변경: 계산된 월드 위치를 '매 프레임' 스크린 위치로 변환 ---
                rectTransform.position = mainCamera.WorldToScreenPoint(currentWorldPos);

                targetScreenPath.Add(rectTransform.position);
                targetTimestamps.Add(Time.time);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (worldPath.Any())
            {
                rectTransform.position = mainCamera.WorldToScreenPoint(worldPath.Last());
                targetScreenPath.Add(rectTransform.position);
                targetTimestamps.Add(Time.time);
            }
            onComplete?.Invoke(targetScreenPath, targetTimestamps);
            Destroy(gameObject);
        }
    }
}