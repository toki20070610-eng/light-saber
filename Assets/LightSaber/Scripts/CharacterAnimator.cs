using UnityEngine;

/// <summary>
/// キャラクターの移動速度を Animator に伝えて、
/// 「止まっている → 待機アニメ」「動いている → 走りアニメ」を切り替えます。
/// プレイヤーにも敵にも使えます（キャラクターの一番上のオブジェクトに付ける）。
/// </summary>
public class CharacterAnimator : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");

    [Tooltip("アニメーションの切り替わりのなめらかさ（秒）")]
    [SerializeField] float dampTime = 0.1f;

    Animator animator;
    Health health;
    Vector3 lastPosition;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();
        lastPosition = transform.position;
    }

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null || Time.deltaTime <= 0f) return;

        // 前のフレームからどれだけ動いたか（上下は無視）から速さを計算する
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float speed = (health != null && health.IsDead) ? 0f : delta.magnitude / Time.deltaTime;
        animator.SetFloat(SpeedId, speed, dampTime, Time.deltaTime);
    }
}
