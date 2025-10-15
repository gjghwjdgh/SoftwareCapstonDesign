using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("오브젝트 연결")]
    [Tooltip("자식으로 있는 Main Camera 오브젝트를 여기에 끌어다 놓으세요.")]
    public Transform cameraHead;

    [Header("설정")]
    public float mouseSensitivity = 100.0f;
    public float moveSpeed = 5.0f;

    private float xRotation = 0f;

    void Start()
    {
        // cameraHead가 연결되었는지 확인
        if (cameraHead == null)
        {
            Debug.LogError("PlayerController: cameraHead가 연결되지 않았습니다! Main Camera를 연결해주세요.");
            this.enabled = false; // 스크립트 비활성화
            return;
        }

        // 시작 시 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- 1. 마우스 입력으로 회전 처리 ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 몸통(이 스크립트가 붙은 오브젝트)은 좌우로만 회전
        transform.Rotate(Vector3.up * mouseX);

        // 머리(cameraHead)는 위아래로만 회전
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // 90도 이상 고개가 꺾이지 않도록 제한
        cameraHead.localRotation = Quaternion.Euler(xRotation, 0f, 0f);


        // --- 2. 키보드 입력으로 이동 처리 ---
        float h = Input.GetAxis("Horizontal"); // A, D 키
        float v = Input.GetAxis("Vertical");   // W, S 키

        transform.Translate(new Vector3(h, 0, v) * moveSpeed * Time.deltaTime);


        // --- 3. ESC 키로 커서 잠금 해제 ---
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}