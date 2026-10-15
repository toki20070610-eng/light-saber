using UnityEngine;

/// <summary>
/// 人型モデルの見た目を管理します。プレイヤーにも敵にも使えます
/// （キャラクターの一番上のオブジェクトに付ける）。
///
/// ・移動速度を Animator に伝えて「待機 ⇔ 走り」アニメを切り替える
/// ・モデルの位置を毎フレーム固定して、沈んだりズレたりしないようにする
/// ・腰の前後左右のズレを消して、その場で走るようにする（ワープ防止）
/// </summary>
// 他のスクリプト（SaberHandIK など）より先に LateUpdate を実行する
[DefaultExecutionOrder(-100)]
public class CharacterAnimator : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");

    [Tooltip("アニメーションの切り替わりのなめらかさ（秒）")]
    [SerializeField] float dampTime = 0.1f;

    [Tooltip("モデルの高さの微調整。足が地面に埋まるならプラス、浮くならマイナス")]
    [SerializeField] float modelHeightOffset = 0f;

    [Tooltip("腰が前後左右にズレないよう固定する。走りアニメが前に進んでしまうとき（ワープする）に効く")]
    [SerializeField] bool keepHipsInPlace = true;

    Animator animator;
    Transform model;
    Vector3 modelLocalPosition;
    Quaternion modelLocalRotation;
    Transform hips;
    Vector3 hipsHome;          // モデルから見た、最初の腰の位置
    Health health;
    Vector3 lastPosition;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();
        lastPosition = transform.position;

        if (animator != null)
        {
            animator.applyRootMotion = false; // 移動はスクリプトが担当する
            model = animator.transform;
            modelLocalPosition = model.localPosition;
            modelLocalRotation = model.localRotation;

            if (animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) hipsHome = model.InverseTransformPoint(hips.position);
            }
        }
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

    void LateUpdate()
    {
        // アニメーションなどでモデルの位置がズレても、毎フレーム元の場所に戻す
        if (model == null) return;
        model.localPosition = modelLocalPosition + Vector3.up * modelHeightOffset;
        model.localRotation = modelLocalRotation;

        // 腰の前後左右だけ最初の位置に戻す（上下の揺れはそのまま残す）
        if (keepHipsInPlace && hips != null)
        {
            Vector3 local = model.InverseTransformPoint(hips.position);
            local.x = hipsHome.x;
            local.z = hipsHome.z;
            hips.position = model.TransformPoint(local);
        }
    }
}
