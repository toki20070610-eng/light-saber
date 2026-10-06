using UnityEngine;

/// <summary>
/// 人型モデルの手を、ライトセーバーの柄（つか）に自動で合わせます。
///
/// 「IK（インバースキネマティクス）」という仕組みを使っています。
/// 普通は「肩→ひじ→手」の順に角度を決めますが、IK は逆に
/// 「手をここに置きたい」と指定すると、ひじや肩の角度を自動で計算してくれます。
///
/// これにより、斬る動きは今まで通り Lightsaber.cs が作り、
/// 腕はそれに合わせて自然についてくるようになります。
/// （Animator コントローラーの「IK Pass」がオンになっている必要があります。
///   メニューの「キャラクターモデルを設定」を使えば自動でオンになります）
/// </summary>
[RequireComponent(typeof(Animator))]
public class SaberHandIK : MonoBehaviour
{
    [Tooltip("手を合わせる場所（Lightsaber オブジェクト）")]
    [SerializeField] Transform saberGrip;

    [Header("右手")]
    [SerializeField, Range(0f, 1f)] float rightHandWeight = 1f;
    [Tooltip("手首の向きも合わせる強さ。0 だとアニメーションの向きのまま")]
    [SerializeField, Range(0f, 1f)] float rightHandRotationWeight = 0f;
    [Tooltip("手首の向きの補正（再生中にいじって、自然に見える角度を探してください）")]
    [SerializeField] Vector3 rightHandRotationOffset = Vector3.zero;

    [Header("左手（両手持ちにしたいとき）")]
    [SerializeField, Range(0f, 1f)] float leftHandWeight = 0f;
    [Tooltip("柄のどのあたりを左手で握るか（マイナスで柄の下側）")]
    [SerializeField] float leftHandGripOffset = -0.12f;

    Animator animator;
    Health health;

    void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponentInParent<Health>();
        if (saberGrip == null)
        {
            Lightsaber saber = transform.parent != null ? transform.parent.GetComponentInChildren<Lightsaber>() : null;
            if (saber != null) saberGrip = saber.transform;
        }
    }

    // Animator が IK を計算するタイミングで Unity が呼んでくれる
    void OnAnimatorIK(int layerIndex)
    {
        if (saberGrip == null) return;

        float alive = (health != null && health.IsDead) ? 0f : 1f; // 倒れたら手を離す

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight * alive);
        animator.SetIKPosition(AvatarIKGoal.RightHand, saberGrip.position);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandRotationWeight * alive);
        animator.SetIKRotation(AvatarIKGoal.RightHand, saberGrip.rotation * Quaternion.Euler(rightHandRotationOffset));

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight * alive);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, saberGrip.position + saberGrip.up * leftHandGripOffset);
    }
}
