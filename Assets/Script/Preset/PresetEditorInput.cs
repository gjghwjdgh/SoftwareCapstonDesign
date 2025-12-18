using UnityEngine;
using UnityEngine.InputSystem;

public class PresetEditorInput : MonoBehaviour
{
    [Header("연결")]
    public PresetManager presetManager;
    public PathVisualizer pathVisualizer;

    [Header("에디터 설정")]
    public bool enableEditorInput = true;
    public bool createVirtualRoom = true;

    [Header("카메라 이동 설정")]
    public float moveSpeed = 2.0f;
    public float lookSpeed = 0.2f;

    private GameObject roomRoot;
    private float rotationX = 0;
    private float rotationY = 0;

    void Start()
    {
#if UNITY_EDITOR
        if (enableEditorInput)
        {
            if (createVirtualRoom) CreateSmallRoom();

            Debug.Log("🟦 [에디터 모드 활성화]");
            Debug.Log("   - 앞(파랑) / 뒤(빨강) / 좌(초록) / 우(노랑)");
            Debug.Log("   - 조작: 우클릭+WASD (이동), 좌클릭 (배치)");
        }
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!enableEditorInput) return;

        // 1. 카메라 이동 (우클릭 상태)
        if (Mouse.current.rightButton.isPressed)
        {
            HandleCameraMovement();
        }

        // 2. 타겟 배치 (좌클릭)
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                HandleEditorClick();
            }
        }
#endif
    }

    // --- [기능 1] 알록달록한 방 만들기 ---
    void CreateSmallRoom()
    {
        roomRoot = new GameObject("Virtual_Room");
        roomRoot.transform.position = Vector3.zero;

        // 색상별 재질 생성
        Material floorMat = CreateMat(new Color(0.2f, 0.2f, 0.2f)); // 바닥: 짙은 회색
        Material frontMat = CreateMat(new Color(0.3f, 0.3f, 0.8f)); // 앞: 파랑
        Material backMat = CreateMat(new Color(0.8f, 0.3f, 0.3f)); // 뒤: 빨강
        Material leftMat = CreateMat(new Color(0.3f, 0.8f, 0.3f)); // 좌: 초록
        Material rightMat = CreateMat(new Color(0.8f, 0.8f, 0.3f)); // 우: 노랑

        // 1. 바닥 (Y = -1.5m, 5m x 5m)
        CreatePlane("Floor", new Vector3(0, -1.5f, 0), new Vector3(5, 1, 5), Vector3.zero, floorMat);

        // 2. 벽 4개 (높이 3m)
        // 앞벽 (파랑)
        CreatePlane("Wall_Front", new Vector3(0, 0, 2.5f), new Vector3(5, 1, 3), new Vector3(-90, 0, 0), frontMat);
        // 뒷벽 (빨강)
        CreatePlane("Wall_Back", new Vector3(0, 0, -2.5f), new Vector3(5, 1, 3), new Vector3(-90, 0, 0), backMat);
        // 왼쪽벽 (초록)
        CreatePlane("Wall_Left", new Vector3(-2.5f, 0, 0), new Vector3(5, 1, 3), new Vector3(-90, 90, 0), leftMat);
        // 오른쪽벽 (노랑)
        CreatePlane("Wall_Right", new Vector3(2.5f, 0, 0), new Vector3(5, 1, 3), new Vector3(-90, 90, 0), rightMat);
    }

    // 재질 생성 헬퍼 함수
    // 재질 생성 헬퍼 함수 (수정됨: 핑크색 에러 방지)
    Material CreateMat(Color color)
    {
        // 1. URP용 쉐이더 이름을 먼저 찾습니다. (AR 프로젝트용)
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        // 2. 만약 URP가 아니라면 기본 Standard 쉐이더를 찾습니다.
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);

        // 3. 색상 적용 (URP는 _BaseColor, 일반은 _Color 속성을 씁니다)
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color); // URP용
        }
        else
        {
            mat.color = color; // 일반용
        }

        // 반사광 줄이기 (선택사항)
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
        else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.0f);

        return mat;
    }

    void CreatePlane(string name, Vector3 pos, Vector3 scale, Vector3 rot, Material mat)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = name;
        plane.transform.SetParent(roomRoot.transform);
        plane.transform.position = pos;
        plane.transform.localScale = new Vector3(scale.x * 0.1f, 1, scale.z * 0.1f);
        plane.transform.eulerAngles = rot;
        plane.GetComponent<MeshRenderer>().material = mat;
    }

    // --- [기능 2] 카메라 이동 ---
    void HandleCameraMovement()
    {
        Transform camTr = Camera.main.transform;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        rotationX += -mouseDelta.y * lookSpeed;
        rotationY += mouseDelta.x * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -90, 90);

        camTr.rotation = Quaternion.Euler(rotationX, rotationY, 0);

        Vector3 moveDir = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) moveDir += camTr.forward;
        if (Keyboard.current.sKey.isPressed) moveDir -= camTr.forward;
        if (Keyboard.current.aKey.isPressed) moveDir -= camTr.right;
        if (Keyboard.current.dKey.isPressed) moveDir += camTr.right;
        if (Keyboard.current.eKey.isPressed) moveDir += Vector3.up;
        if (Keyboard.current.qKey.isPressed) moveDir -= Vector3.up;

        camTr.position += moveDir * moveSpeed * Time.deltaTime;
    }

    // --- [기능 3] 타겟 배치 ---
    void HandleEditorClick()
    {
        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (pathVisualizer.startPoint == null)
            {
                GameObject startObj = Instantiate(presetManager.startPointPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                pathVisualizer.startPoint = startObj.transform;
                if (GameUIManager.Instance != null && GameUIManager.Instance.targetParent != null)
                    startObj.transform.SetParent(GameUIManager.Instance.targetParent);
                Debug.Log("🚩 시작점 배치됨");
            }
            else
            {
                GameObject targetObj = Instantiate(presetManager.targetPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                if (GameUIManager.Instance != null && GameUIManager.Instance.targetParent != null)
                    targetObj.transform.SetParent(GameUIManager.Instance.targetParent);

                pathVisualizer.targets.Add(targetObj.transform);

                if (GameUIManager.Instance != null)
                    GameUIManager.Instance.SetAlgorithmMode((int)GameUIManager.Instance.pathSolver.currentMode);

                Debug.Log($"📦 타겟 배치됨 ({hit.collider.name})");
            }
        }
    }
}