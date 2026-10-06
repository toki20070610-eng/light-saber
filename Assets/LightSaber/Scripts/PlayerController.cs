using UnityEngine;

/// <summary>
/// プレイヤーの操作。移動・ジャンプ・攻撃・ガードを担当します。
/// CharacterController（Unity標準の「歩くキャラ用」部品）を使って動かします。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("移動")]
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float runSpeed = 8f;
    [Tooltip("ガード中・攻撃中の移動速度の倍率")]
    [SerializeField] float actionMoveRate = 0.35f;
    [Tooltip("1秒間に回転できる角度")]
    [SerializeField] float turnSpeed = 720f;

    [Header("ジャンプ")]
    [SerializeField] float jumpHeight = 1.5f;
    [SerializeField] float gravity = -20f;

    [Header("戦闘")]
    [Tooltip("攻撃・ガードの時に、この距離以内の一番近い敵の方を向く")]
    [SerializeField] float lockOnRange = 5f;

    [Tooltip("移動の向きの基準にするカメラ（空ならメインカメラ）")]
    [SerializeField] Transform cameraTransform;

    CharacterController controller;
    Lightsaber saber;
    Health health;
    float verticalVelocity; // 上下方向の速度

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        saber = GetComponentInChildren<Lightsaber>();
        health = GetComponent<Health>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (health != null) health.Died += OnDied;
    }

    void Update()
    {
        // ---- ① 移動方向を求める（カメラの向きを基準にする） ----
        Vector2 input = GameInput.Move;
        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        Vector3 moveDir = forward.normalized * input.y + right.normalized * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        float speed = GameInput.RunHeld ? runSpeed : walkSpeed;

        // ---- ② ライトセーバー操作 ----
        if (saber != null)
        {
            if (GameInput.ToggleSaberPressed) saber.Toggle();
            saber.SetGuard(GameInput.GuardHeld);

            if (GameInput.AttackPressed && saber.Swing()) FaceNearestEnemy(true);
            if (saber.IsGuarding) FaceNearestEnemy(false);
            if (saber.IsGuarding || saber.IsSwinging) speed *= actionMoveRate;
        }

        // ---- ③ 進む方向を向く（攻撃・ガード中以外） ----
        bool busy = saber != null && (saber.IsGuarding || saber.IsSwinging);
        if (!busy && moveDir.sqrMagnitude > 0.01f) TurnTowards(moveDir, false);

        // ---- ④ 重力とジャンプ ----
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f; // 地面に押し付けておく
        if (controller.isGrounded && GameInput.JumpPressed)
        {
            // 物理の公式 v = √(2gh) でジャンプの初速を計算
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalVelocity += gravity * Time.deltaTime;

        // ---- ⑤ 実際に動かす ----
        Vector3 velocity = moveDir * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void TurnTowards(Vector3 direction, bool instant)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = instant
            ? target
            : Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    /// <summary>近くの敵の方を向く（ソフトロックオン）</summary>
    void FaceNearestEnemy(bool instant)
    {
        EnemyAI nearest = null;
        float nearestDist = lockOnRange;
        foreach (EnemyAI enemy in EnemyAI.All)
        {
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = enemy;
            }
        }
        if (nearest != null) TurnTowards(nearest.transform.position - transform.position, instant);
    }

    void OnDied()
    {
        if (saber != null) saber.SetGuard(false);
        enabled = false; // このスクリプトを止める（操作できなくなる）
    }
}
