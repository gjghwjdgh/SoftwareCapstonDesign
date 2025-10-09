using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class UIPursuitMover : MonoBehaviour
{
    public Image helperPrefab;
    public Transform canvasTransform;
    private Image currentHelperInstance;
    private Coroutine movementCoroutine;

    public void StartMovement(UILineConnector lineToFollow, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);

        // --- ▼ 여기가 추가된 부분입니다 ▼ ---
        // 만약 이전에 움직이던 UI 도우미가 남아있다면, 확실하게 파괴합니다.
        if (currentHelperInstance != null) Destroy(currentHelperInstance.gameObject);
        // --- ▲ 여기가 추가된 부분입니다 ▲ ---

        currentHelperInstance = Instantiate(helperPrefab, canvasTransform);
        movementCoroutine = StartCoroutine(MoveOnScreen(currentHelperInstance, lineToFollow, duration, onComplete));
    }

    private IEnumerator MoveOnScreen(Image helper, UILineConnector lineToFollow, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        // ... (내부 코드는 이전과 동일)
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (helper == null || lineToFollow == null) { onComplete?.Invoke(null, null); yield break; }
            Vector2 startScreenPos = Camera.main.WorldToScreenPoint(lineToFollow.StartPoint3D.position);
            Vector2 targetScreenPos = Camera.main.WorldToScreenPoint(lineToFollow.Target3D.position);
            float progress = elapsedTime / duration;
            Vector2 currentPos = Vector2.Lerp(startScreenPos, targetScreenPos, progress);
            helper.rectTransform.position = currentPos;
            targetScreenPath.Add(currentPos);
            targetTimestamps.Add(Time.time);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        if (helper != null) Destroy(helper.gameObject);
        onComplete?.Invoke(targetScreenPath, targetTimestamps);
    }
}