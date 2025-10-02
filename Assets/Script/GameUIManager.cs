using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [Header("������ ��ũ��Ʈ ����")]
    public PathManager pathManager;
    public PathVisualizer pathVisualizer;
    public PursuitMover pursuitMover;

    [Header("����")]
    public float travelDuration = 2.0f;

    // --- �߰��� �κ�: Ű���� �Է��� �� ������ ���� ---
    void Update()
    {
        // --- �۽�Ʈ ���� Ű (���� 1, 2, 3, 4) ---
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            OnStartPursuitClicked(0); // ù ��° Ÿ�� (�ε��� 0)
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            OnStartPursuitClicked(1); // �� ��° Ÿ�� (�ε��� 1)
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            OnStartPursuitClicked(2); // �� ��° Ÿ�� (�ε��� 2)
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            OnStartPursuitClicked(3); // �� ��° Ÿ�� (�ε��� 3)
        }

        // --- �� ��ȯ Ű (Tab) ---
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            OnSwitchViewClicked();
        }
    }
    // --- ������� �߰� ---

    /// <summary>
    /// �۽�Ʈ ���� ��ư Ŭ�� �� ȣ��� �Լ� (UI ��ư �Ǵ� Ű����� ȣ��)
    /// </summary>
    public void OnStartPursuitClicked(int targetIndex)
    {
        // PathManager�� Ÿ�� �������� ū �ε����� ��û�� ��츦 ����
        if (targetIndex >= pathManager.targets.Count)
        {
            Debug.LogWarning($"Ÿ�� �ε��� {targetIndex}�� �������� �ʽ��ϴ�.");
            return;
        }

        // 1. PathManager���� ��� ������ ��û
        var pathPoints = pathManager.GetPathPoints(targetIndex);
        if (pathPoints == null) return;

        // 2. PathVisualizer���� �ش� ��θ� ���̶���Ʈ�ϵ��� ����
        pathVisualizer.HighlightPath(targetIndex);

        // 3. PursuitMover���� �� ��η� �̵��� �����϶�� ����
        pursuitMover.StartMovement(pathPoints, travelDuration);
    }

    /// <summary>
    /// �� ��� ��ȯ ��ư Ŭ�� �� ȣ��� �Լ� (UI ��ư �Ǵ� Ű����� ȣ��)
    /// </summary>
    public void OnSwitchViewClicked()
    {
        pathVisualizer.SwitchViewMode();
    }
}