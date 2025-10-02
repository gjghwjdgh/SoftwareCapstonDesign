using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    [Tooltip("카메라의 초당 이동 속도입니다.")]
    public float moveSpeed = 5.0f;
    [Tooltip("마우스 감도입니다.")]
    public float mouseSensitivity = 100.0f;

    // 내부적으로 사용할 회전값 변수
    private float currentXRotation = 0f; // 상하 회전 (Pitch)
    private float currentYRotation = 0f; // 좌우 회전 (Yaw)

    void Start()
    {
        // 게임 시작 시 마우스 커서를 화면 중앙에 고정하고 숨깁니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- 1. 회전 기능 (마우스 입력) ---
        // 마우스의 좌우 움직임(X)과 상하 움직임(Y) 값을 가져옵니다.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 좌우 회전값(Y축 기준)을 누적합니다.
        currentYRotation += mouseX;
        // 상하 회전값(X축 기준)을 누적합니다. (마우스를 위로 올리면 화면이 내려가는 것을 방지하기 위해 -= 사용)
        currentXRotation -= mouseY;

        // 상하 회전 각도를 -90도 ~ 90도 사이로 제한하여 카메라가 거꾸로 뒤집히는 것을 방지합니다.
        currentXRotation = Mathf.Clamp(currentXRotation, -90f, 90f);

        // 계산된 회전값을 카메라에 실제로 적용합니다.
        // Quaternion.Euler를 사용하면 짐벌락(Gimbal Lock) 현상 없이 안전하게 회전할 수 있습니다.
        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);


        // --- 2. 이동 기능 (키보드 입력) ---
        float horizontalInput = Input.GetAxis("Horizontal"); // A, D
        float verticalInput = Input.GetAxis("Vertical");     // W, S

        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput);

        // [중요] transform.position 대신 transform.Translate를 사용합니다.
        // 이렇게 하면 월드 좌표계가 아닌 '카메라가 바라보는 방향'을 기준으로 이동합니다.
        // 즉, W를 누르면 항상 카메라의 '앞으로' 이동합니다.
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);


        // --- 3. 커서 잠금 해제 기능 ---
        // 'ESC' 키를 누르면 마우스 커서 잠금을 해제합니다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}