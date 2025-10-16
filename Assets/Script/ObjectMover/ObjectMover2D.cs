using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Image))]
public class ObjectMover2D : MonoBehaviour
{
    public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public IEnumerator MoveOnScreen(UILineConnector lineToFollow, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        List<Vector2> targetScreenPath = new List<Vector2>();
        List<float> targetTimestamps = new List<float>();
        float elapsedTime = 0f;
        Camera mainCamera = Camera.main;

        try
        {
            while (elapsedTime < duration)
            {
                if (lineToFollow == null) yield break;

                Vector2 startScreenPos = mainCamera.WorldToScreenPoint(lineToFollow.StartPoint3D.position);
                Vector2 targetScreenPos = mainCamera.WorldToScreenPoint(lineToFollow.Target3D.position);
                float progress = elapsedTime / duration;
                float curveProgress = speedCurve.Evaluate(progress);
                Vector2 currentPos = Vector2.Lerp(startScreenPos, targetScreenPos, curveProgress);
                rectTransform.position = currentPos;

                targetScreenPath.Add(currentPos);
                targetTimestamps.Add(Time.time);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            onComplete?.Invoke(targetScreenPath, targetTimestamps);
            Destroy(gameObject);
        }
    }
}