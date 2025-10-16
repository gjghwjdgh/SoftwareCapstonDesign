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
        Vector3 startPos = path[0];
        Vector3 endPos = path[path.Count - 1];
        Camera mainCamera = Camera.main;

        try
        {
            while (elapsedTime < duration)
            {
                float progress = elapsedTime / duration;
                float curveProgress = speedCurve.Evaluate(progress);
                transform.position = Vector3.Lerp(startPos, endPos, curveProgress);

                targetScreenPath.Add(mainCamera.WorldToScreenPoint(transform.position));
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