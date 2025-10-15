using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIPursuitMover : MonoBehaviour
{
    public Image helperPrefab; // ObjectMover2D.cs가 붙어있는 프리팹
    public Transform canvasTransform;

    // UI 도우미 객체를 생성하고, 이동 코루틴을 시작시킨 후 아무것도 하지 않음
    public void StartMovement(UILineConnector lineToFollow, float duration, System.Action<List<Vector2>, List<float>> onComplete)
    {
        Image helperInstance = Instantiate(helperPrefab, canvasTransform);
        ObjectMover2D mover = helperInstance.GetComponent<ObjectMover2D>();
        if (mover != null)
        {
            StartCoroutine(mover.MoveOnScreen(lineToFollow, duration, onComplete));
        }
        else
        {
            Debug.LogError("UI Helper Prefab에 ObjectMover2D 스크립트가 없습니다!");
            Destroy(helperInstance.gameObject);
        }
    }
}