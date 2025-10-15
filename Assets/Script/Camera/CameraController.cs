using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    public float moveSpeed = 5.0f;
    public float mouseSensitivity = 100.0f;

    // 카메라의 최종 각도를 저장할 변수들
    private float yRotation = 0.0f;
    private float xRotation = 0.0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 시작 시 카메라의 현재 각도를 초기값으로 설정 (시작 시 튀는 현상 방지)
        Vector3 startAngles = transform.eulerAngles;
        yRotation = startAngles.y;
        xRotation = startAngles.x;
    }

    void LateUpdate()
    {
        // 1. 마우스 입력값을 받습니다.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 2. 입력값을 기반으로 최종 목표 각도를 변수에 누적합니다.
        yRotation += mouseX;
        xRotation -= mouseY;

        // 3. 상하 각도를 -90도와 90도 사이로 제한합니다.
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 4. 계산된 최종 각도를 한 번에 카메라에 적용합니다.
        //    이것이 회전을 제어하는 유일한 코드 라인입니다.
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);


        // 이동 로직 (이전과 동일)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        transform.Translate(new Vector3(h, 0, v) * moveSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}