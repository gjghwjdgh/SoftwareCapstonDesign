using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Image))]
public class ObjectMover2D : MonoBehaviour
{
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // 2D 스크린 공간의 경로를 따라 이동하고, 완료되면 콜백을 호출한 뒤 스스로를 파괴합니다.
    public IEnumerator MoveOnScreen(UILineConnector lineToFollow, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (lineToFollow == null) // 라인이 중간에 사라지는 경우 대비
            {
                onComplete?.Invoke(null, null);
                Destroy(gameObject);
                yield break;
            }

            // 3D 좌표를 실시간으로 2D 스크린 좌표로 변환
            Vector2 startScreenPos = Camera.main.WorldToScreenPoint(lineToFollow.StartPoint3D.position);
            Vector2 targetScreenPos = Camera.main.WorldToScreenPoint(lineToFollow.Target3D.position);

            float progress = elapsedTime / duration;
            Vector2 currentPos = Vector2.Lerp(startScreenPos, targetScreenPos, progress);
            rectTransform.position = currentPos;

            // 경로 기록
            targetScreenPath.Add(currentPos);
            targetTimestamps.Add(Time.time);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 이동 완료 후 콜백 호출 및 자신 파괴
        onComplete?.Invoke(targetScreenPath, targetTimestamps);
        Destroy(gameObject);
    }
}