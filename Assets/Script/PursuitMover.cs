using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PursuitMover : MonoBehaviour
{
    [Header("������Ʈ ����")]
    public GameObject helperPrefab;

    private GameObject currentHelperInstance;
    private Coroutine movementCoroutine;

    /// <summary>
    /// �־��� ��θ� ���� ������ �ð� ���� ����� ��ü�� �̵���ŵ�ϴ�.
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
            // �ð��� ���� �����(0.0 ~ 1.0) ���
            float progress = elapsedTime / duration;
            // ������� �´� ��λ��� �ε��� ���
            int currentIndex = Mathf.FloorToInt(progress * (path.Count - 1));

            currentHelperInstance.transform.position = path[currentIndex];

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // �̵� �Ϸ� �� ��Ȯ�� ���� ��ġ�� ���� �� ��ü �ı�
        currentHelperInstance.transform.position = path[path.Count - 1];
        yield return new WaitForSeconds(1.0f); // 1�� �� �ڵ� �ı�
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