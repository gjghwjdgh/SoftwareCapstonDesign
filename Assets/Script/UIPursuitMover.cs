using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIPursuitMover : MonoBehaviour
{
    [Header("UI 도우미 설정")]
    public Image helperPrefab;
    public Transform canvasTransform;

    private Image currentHelperInstance;
    // --- ▼ 여기가 추가된 부분입니다 (1/2) ▼ ---
    // 현재 실행 중인 이동 명령(코루틴)을 저장할 변수
    private Coroutine movementCoroutine;
    // --- ▲ 여기가 추가된 부분입니다 (1/2) ▲ ---

    public void StartMovement(Transform startPoint, Transform targetPoint, float duration)
    {
        // --- ▼ 여기가 추가된 부분입니다 (2/2) ▼ ---
        // 만약 이전에 실행 중이던 이동 명령이 있다면, 확실하게 중지시킵니다.
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        // --- ▲ 여기가 추가된 부분입니다 (2/2) ▲ ---

        if (currentHelperInstance != null)
        {
            Destroy(currentHelperInstance.gameObject);
        }

        currentHelperInstance = Instantiate(helperPrefab, canvasTransform);
        // 새로운 이동 명령을 시작하고, 그 명령을 변수에 저장합니다.
        movementCoroutine = StartCoroutine(MoveOnScreen(startPoint, targetPoint, duration));
    }

    private IEnumerator MoveOnScreen(Transform startPoint, Transform targetPoint, float duration)
    {
        float elapsedTime = 0f;
        Camera mainCamera = Camera.main;

        while (elapsedTime < duration)
        {
            // currentHelperInstance가 외부에서 파괴되었을 경우를 대비한 안전장치
            if (currentHelperInstance == null)
            {
                yield break; // 코루틴 즉시 종료
            }

            Vector2 startScreenPos = mainCamera.WorldToScreenPoint(startPoint.position);
            Vector2 targetScreenPos = mainCamera.WorldToScreenPoint(targetPoint.position);

            float progress = elapsedTime / duration;
            
            currentHelperInstance.rectTransform.position = Vector2.Lerp(startScreenPos, targetScreenPos, progress);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (currentHelperInstance != null)
        {
            Destroy(currentHelperInstance.gameObject);
        }
    }
}