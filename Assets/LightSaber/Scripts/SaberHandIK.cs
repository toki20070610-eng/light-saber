using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 人型モデルにライトセーバーを「握らせる」スクリプト。
///
/// 毎フレーム、アニメーションの後に次の3つを行います。
///  1. IK（インバースキネマティクス）で、右手をセーバーの位置へ伸ばす
///     ※「手をここに置きたい」と指定すると、ひじや肩の角度を自動で計算してくれる仕組み
///  2. 手首の向きをセーバーの向きに合わせる
///  3. セーバーの見た目を手のひらに吸着させる
///
/// 3 があるので、走りアニメなどで腕が動いても、セーバーが手から離れません。
/// 斬る動き（角度）は今まで通り Lightsaber.cs が作ります。
/// </summary>
[RequireComponent(typeof(Animator))]
public class SaberHandIK : MonoBehaviour
{
    [Tooltip("ライトセーバー（Lightsaber コンポーネントが付いたオブジェクト）")]
    [SerializeField] Transform saberGrip;

    [Header("右手")]
    [Tooltip("右手をセーバーへ伸ばす強さ")]
    [SerializeField, Range(0f, 1f)] float rightHandWeight = 1f;
    [Tooltip("手首の向きをセーバーに合わせる")]
    [SerializeField] bool alignHand = true;
    [Tooltip("手首の向きの微調整（X, Y, Z の角度）")]
    [SerializeField] Vector3 handRotationOffset = Vector3.zero;
    [Tooltip("セーバーの見た目を手のひらに吸着させる")]
    [SerializeField] bool snapSaberToHand = true;

    [Header("左手（両手持ちにしたいとき）")]
    [SerializeField, Range(0f, 1f)] float leftHandWeight = 0f;
    [Tooltip("柄のどのあたりを左手で握るか（マイナスで柄の下側）")]
    [SerializeField] float leftHandGripOffset = -0.12f;

    Animator animator;
    Health health;
    Lightsaber saber;
    Transform visualRoot;

    // 手の骨
    Transform hand;
    Transform middleProximal;
    Vector3 localFingerDir;   // 手首 → 中指の付け根（手の骨から見た向き）
    Vector3 localKnuckleDir;  // 小指の付け根 → 人差し指の付け根
    bool hasHandAxes;
    Vector3 palmOffset;       // 手首 → 手のひら（前のフレームの値）

    bool IsAlive => health == null || !health.IsDead;

    void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponentInParent<Health>();

        if (saberGrip == null && transform.parent != null)
        {
            Lightsaber found = transform.parent.GetComponentInChildren<Lightsaber>();
            if (found != null) saberGrip = found.transform;
        }
        if (saberGrip != null) saber = saberGrip.GetComponent<Lightsaber>();

        SetupHandBones();
        if (snapSaberToHand) SetupVisualRoot();
    }

    void SetupHandBones()
    {
        if (!animator.isHuman) return;

        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        middleProximal = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (hand == null || middleProximal == null || index == null || little == null) return;

        // 手の中の骨どうしの位置関係は変わらないので、最初に一度だけ測っておく
        localFingerDir = hand.InverseTransformDirection(middleProximal.position - hand.position).normalized;
        localKnuckleDir = hand.InverseTransformDirection(index.position - little.position).normalized;
        palmOffset = (middleProximal.position - hand.position) * 0.7f;
        hasHandAxes = true;
    }

    /// <summary>
    /// セーバーの「柄と刃」をまとめた Visual オブジェクトを作る。
    /// 角度は Lightsaber の回転に従い、位置だけを手のひらに合わせる。
    /// </summary>
    void SetupVisualRoot()
    {
        if (saberGrip == null || saber == null || !hasHandAxes) return;

        Transform visual = saberGrip.Find("Visual");
        if (visual == null)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(saberGrip, false);

            var children = new List<Transform>();
            foreach (Transform child in saberGrip)
            {
                if (child != visual) children.Add(child);
            }
            foreach (Transform child in children) child.SetParent(visual, false);
        }
        visualRoot = visual;
        saber.SetVisualRoot(visualRoot);
    }

    // ① アニメーションの計算中に呼ばれる：腕を伸ばす
    void OnAnimatorIK(int layerIndex)
    {
        if (saberGrip == null) return;
        float alive = IsAlive ? 1f : 0f;

        // 手のひらがセーバーの位置に来るように、手首の目標を少しずらす
        Vector3 wristTarget = saberGrip.position - (hasHandAxes ? palmOffset : Vector3.zero);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight * alive);
        animator.SetIKPosition(AvatarIKGoal.RightHand, wristTarget);

        Vector3 gripBase = visualRoot != null ? visualRoot.position : saberGrip.position;
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight * alive);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, gripBase + saberGrip.up * leftHandGripOffset);
    }

    // ②③ アニメーションと IK が終わった後に呼ばれる：仕上げ
    void LateUpdate()
    {
        if (!IsAlive || saberGrip == null) return;

        if (!hasHandAxes) return;

        // ② 手首の向き：指の付け根が「前」、人差し指側が「刃の方向」になるように回す
        if (alignHand)
        {
            Quaternion handFrame = Quaternion.LookRotation(localFingerDir, localKnuckleDir);
            Quaternion saberFrame = Quaternion.LookRotation(saberGrip.forward, saberGrip.up);
            hand.rotation = saberFrame * Quaternion.Inverse(handFrame) * Quaternion.Euler(handRotationOffset);
        }

        // ③ セーバーを手のひらに吸着
        palmOffset = (middleProximal.position - hand.position) * 0.7f;
        if (visualRoot != null) visualRoot.position = hand.position + palmOffset;
    }
}
