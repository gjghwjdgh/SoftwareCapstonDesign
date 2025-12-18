using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIPursuitMover : MonoBehaviour
{
    public Image helperPrefab;
    public Transform canvasTransform;

    // -- 핵심 변경: 2D 스크린 경로 대신 3D 월드 경로를 받습니다 ---
    public void StartMovement(List<Vector3> worldPath, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        Image helperInstance = Instantiate(helperPrefab, canvasTransform);
        ObjectMover2D mover = helperInstance.GetComponent<ObjectMover2D>();
        if (mover != null)
        {
            StartCoroutine(mover.MoveOnScreen(worldPath, duration, onComplete));
        }
        else
        {
            Debug.LogError("UI Helper Prefab에 ObjectMover2D 스크립트가 없습니다!");
            onComplete?.Invoke(null, null);
            Destroy(helperInstance.gameObject);
        }
    }
}