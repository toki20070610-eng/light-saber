using UnityEngine;

/// <summary>
/// 三人称カメラ。プレイヤーの後ろからついていき、マウスで周りを見回せます。
/// メインカメラに付けて使います。
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Tooltip("追いかける対象（空なら Player タグのオブジェクト）")]
    [SerializeField] Transform target;
    [SerializeField] float distance = 5f;
    [Tooltip("注視点の高さ（対象の中心からの高さ）")]
    [SerializeField] float focusHeight = 0.8f;
    [SerializeField] float mouseSensitivity = 3f;
    [SerializeField] float minPitch = -20f;
    [SerializeField] float maxPitch = 60f;
    [Tooltip("壁にめり込まないための判定の太さ")]
    [SerializeField] float collisionRadius = 0.25f;

    float yaw;          // 左右の角度
    float pitch = 15f;  // 上下の角度

    void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) yaw = target.eulerAngles.y;
        LockCursor(true);
    }

    // LateUpdate は全ての Update の後に呼ばれる。キャラが動いた後にカメラを動かすため
    void LateUpdate()
    {
        if (target == null) return;

        // Esc でマウスカーソルを解放、クリックで再びロック
        if (GameInput.CancelPressed) LockCursor(false);
        else if (GameInput.AttackPressed && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = GameInput.Look;
            yaw += look.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * focusHeight;
        Vector3 back = rotation * Vector3.back;

        // 後ろに壁があればカメラを手前に寄せる（キャラクターは無視）
        float dist = distance;
        foreach (RaycastHit hit in Physics.SphereCastAll(focus, collisionRadius, back, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider is CharacterController) continue;
            if (hit.distance < dist) dist = hit.distance;
        }

        transform.position = focus + back * dist;
        transform.rotation = rotation;
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
