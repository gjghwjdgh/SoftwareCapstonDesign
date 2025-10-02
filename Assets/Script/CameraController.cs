using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("ī�޶� ����")]
    [Tooltip("ī�޶��� �ʴ� �̵� �ӵ��Դϴ�.")]
    public float moveSpeed = 5.0f;
    [Tooltip("���콺 �����Դϴ�.")]
    public float mouseSensitivity = 100.0f;

    // ���������� ����� ȸ���� ����
    private float currentXRotation = 0f; // ���� ȸ�� (Pitch)
    private float currentYRotation = 0f; // �¿� ȸ�� (Yaw)

    void Start()
    {
        // ���� ���� �� ���콺 Ŀ���� ȭ�� �߾ӿ� �����ϰ� ����ϴ�.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- 1. ȸ�� ��� (���콺 �Է�) ---
        // ���콺�� �¿� ������(X)�� ���� ������(Y) ���� �����ɴϴ�.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // �¿� ȸ����(Y�� ����)�� �����մϴ�.
        currentYRotation += mouseX;
        // ���� ȸ����(X�� ����)�� �����մϴ�. (���콺�� ���� �ø��� ȭ���� �������� ���� �����ϱ� ���� -= ���)
        currentXRotation -= mouseY;

        // ���� ȸ�� ������ -90�� ~ 90�� ���̷� �����Ͽ� ī�޶� �Ųٷ� �������� ���� �����մϴ�.
        currentXRotation = Mathf.Clamp(currentXRotation, -90f, 90f);

        // ���� ȸ������ ī�޶� ������ �����մϴ�.
        // Quaternion.Euler�� ����ϸ� ������(Gimbal Lock) ���� ���� �����ϰ� ȸ���� �� �ֽ��ϴ�.
        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);


        // --- 2. �̵� ��� (Ű���� �Է�) ---
        float horizontalInput = Input.GetAxis("Horizontal"); // A, D
        float verticalInput = Input.GetAxis("Vertical");     // W, S

        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput);

        // [�߿�] transform.position ��� transform.Translate�� ����մϴ�.
        // �̷��� �ϸ� ���� ��ǥ�谡 �ƴ� 'ī�޶� �ٶ󺸴� ����'�� �������� �̵��մϴ�.
        // ��, W�� ������ �׻� ī�޶��� '������' �̵��մϴ�.
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);


        // --- 3. Ŀ�� ��� ���� ��� ---
        // 'ESC' Ű�� ������ ���콺 Ŀ�� ����� �����մϴ�.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}