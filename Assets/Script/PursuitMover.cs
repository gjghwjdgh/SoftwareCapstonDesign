using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    [Header("오브젝트 설정")]
    public GameObject helperPrefab;

    private GameObject currentHelperInstance;
    private Coroutine movementCoroutine;

    /// <summary>
    /// 주어진 경로를 따라 정해진 시간 동안 도우미 객체를 이동시킵니다.
    /// </summary>
    public void StartMovement(List<Vector3> path, float duration)
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        if (currentHelperInstance != null)
        {
            Destroy(currentHelperInstance);
        }

        currentHelperInstance = Instantiate(helperPrefab, path[0], Quaternion.identity);
        movementCoroutine = StartCoroutine(MoveAlongPath(path, duration));
    }

    private IEnumerator MoveAlongPath(List<Vector3> path, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // 시간에 따른 진행률(0.0 ~ 1.0) 계산
            float progress = elapsedTime / duration;
            // 진행률에 맞는 경로상의 인덱스 계산
            int currentIndex = Mathf.FloorToInt(progress * (path.Count - 1));

            currentHelperInstance.transform.position = path[currentIndex];

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 이동 완료 후 정확한 최종 위치로 설정 및 객체 파괴
        currentHelperInstance.transform.position = path[path.Count - 1];
        yield return new WaitForSeconds(1.0f); // 1초 후 자동 파괴
        Destroy(currentHelperInstance);
    }

    public void StopAndClear()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }
        if (currentHelperInstance != null)
        {
            Destroy(currentHelperInstance);
        }
    }
}