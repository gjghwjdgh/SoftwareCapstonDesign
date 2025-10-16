using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIPursuitMover : MonoBehaviour
{
    public Image helperPrefab;
    public Transform canvasTransform;

    // ✨ 약속을 통일: 경로 데이터를 받는 콜백(Action)을 받도록 수정
    public void StartMovement(UILineConnector lineToFollow, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        Image helperInstance = Instantiate(helperPrefab, canvasTransform);
        ObjectMover2D mover = helperInstance.GetComponent<ObjectMover2D>();
        if (mover != null)
        {
            // 받은 콜백을 그대로 ObjectMover2D에게 전달
            StartCoroutine(mover.MoveOnScreen(lineToFollow, duration, onComplete));
        }
        else
        {
            Debug.LogError("UI Helper Prefab에 ObjectMover2D 스크립트가 없습니다!");
            onComplete?.Invoke(null, null);
            Destroy(helperInstance.gameObject);
        }
    }
}