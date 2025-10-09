using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    public float moveSpeed = 5.0f;
    public float mouseSensitivity = 100.0f;

    // IsLocked 프로퍼티 제거
    private float currentXRotation = 0f;
    private float currentYRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // IsLocked 체크 제거
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        currentYRotation += mouseX;
        currentXRotation -= mouseY;
        currentXRotation = Mathf.Clamp(currentXRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(currentXRotation, currentYRotation, 0f);

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput);
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}