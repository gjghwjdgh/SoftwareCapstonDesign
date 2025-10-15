using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ObjectMover3D : MonoBehaviour
{
    // 3D 월드 공간의 경로를 따라 이동하고, 완료되면 콜백을 호출한 뒤 스스로를 파괴합니다.
    public IEnumerator MoveAlongPath(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        float elapsedTime = 0f;
        Vector3 startPos = path[0];
        Vector3 endPos = path[path.Count - 1];

        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            transform.position = Vector3.Lerp(startPos, endPos, progress);

            // 경로 기록
            targetScreenPath.Add(Camera.main.WorldToScreenPoint(transform.position));
            targetTimestamps.Add(Time.time);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 이동 완료 후 콜백 호출 및 자신 파괴
        onComplete?.Invoke(targetScreenPath, targetTimestamps);
        Destroy(gameObject);
    }
}