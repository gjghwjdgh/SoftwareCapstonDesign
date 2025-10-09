using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    public GameObject helperPrefab;
    private Coroutine movementCoroutine;
    private GameObject currentHelperInstance;

    public void StartMovement(List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);

        // --- ▼ 여기가 추가된 부분입니다 ▼ ---
        // 만약 이전에 움직이던 도우미가 남아있다면, 확실하게 파괴합니다.
        if (currentHelperInstance != null) Destroy(currentHelperInstance);
        // --- ▲ 여기가 추가된 부분입니다 ▲ ---
        
        currentHelperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);
        movementCoroutine = StartCoroutine(MoveAlongPath(currentHelperInstance, path, duration, onComplete));
    }

    private IEnumerator MoveAlongPath(GameObject helper, List<Vector3> path, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        // ... (내부 코드는 이전과 동일)
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        float elapsedTime = 0f;
        Vector3 startPos = path[0], endPos = path[path.Count - 1];
        while (elapsedTime < duration)
        {
            if (helper == null) { onComplete?.Invoke(null, null); yield break; }
            float progress = elapsedTime / duration;
            helper.transform.position = Vector3.Lerp(startPos, endPos, progress);
            targetScreenPath.Add(Camera.main.WorldToScreenPoint(helper.transform.position));
            targetTimestamps.Add(Time.time);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        if (helper != null) Destroy(helper);
        onComplete?.Invoke(targetScreenPath, targetTimestamps);
    }
}