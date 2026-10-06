using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵の頭脳（AI）。
/// プレイヤーに近づく → 「ため」で予兆を見せる → 斬る、を繰り返します。
/// プレイヤーが斬りかかってくると、一定の確率でガードします。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    /// <summary>生きている敵の一覧（static = 全ての敵で共有される変数）</summary>
    public static readonly List<EnemyAI> All = new List<EnemyAI>();

    // 敵が一斉に斬りかかってこないように、最後に誰かが攻撃を始めた時刻を共有する
    static float lastAttackStartTime = -999f;

    [Header("移動")]
    [SerializeField] float moveSpeed = 2.5f;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] float gravity = -20f;

    [Header("索敵・攻撃")]
    [Tooltip("この距離以内にプレイヤーが入ると動き出す")]
    [SerializeField] float detectRange = 20f;
    [Tooltip("この距離まで近づいたら攻撃する")]
    [SerializeField] float attackRange = 1.8f;
    [Tooltip("攻撃の間隔（秒）")]
    [SerializeField] float attackInterval = 1.6f;
    [Tooltip("斬る前の「ため」の時間。長いほどプレイヤーがガードしやすい")]
    [SerializeField] float windupTime = 0.45f;
    [Tooltip("他の敵の攻撃開始から、最低この秒数は空ける")]
    [SerializeField] float groupAttackGap = 0.6f;

    [Header("防御")]
    [Tooltip("プレイヤーの攻撃をガードする確率（0～1）")]
    [SerializeField, Range(0f, 1f)] float guardChance = 0.35f;

    public Health Health { get; private set; }

    CharacterController controller;
    Lightsaber saber;
    Transform player;
    Health playerHealth;
    Lightsaber playerSaber;

    float nextAttackTime;
    float guardUntil;
    float verticalVelocity;
    bool isAttacking;
    bool reactedToSwing;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        saber = GetComponentInChildren<Lightsaber>();
        Health = GetComponent<Health>();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<Health>();
            playerSaber = p.GetComponentInChildren<Lightsaber>();
        }
        nextAttackTime = Time.time + Random.Range(1f, 2.5f);
        Health.Died += OnDied;
        Health.Damaged += OnDamaged;
    }

    void Update()
    {
        Vector3 move = Vector3.zero;
        bool playerAlive = player != null && (playerHealth == null || !playerHealth.IsDead);

        if (playerAlive)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist < detectRange)
            {
                Turn(toPlayer);
                UpdateGuard(dist);

                if (!isAttacking && !saber.IsGuarding)
                {
                    if (dist > attackRange)
                    {
                        move = toPlayer.normalized * moveSpeed; // 近づく
                    }
                    else if (Time.time >= nextAttackTime && Time.time >= lastAttackStartTime + groupAttackGap)
                    {
                        StartCoroutine(AttackRoutine()); // 攻撃開始
                    }
                }
            }
        }
        else
        {
            saber.SetGuard(false);
        }

        // 重力
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    void Turn(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    /// <summary>プレイヤーが斬りかかってきたら、確率でガードする</summary>
    void UpdateGuard(float dist)
    {
        bool playerSwinging = playerSaber != null && playerSaber.IsSwinging;
        if (playerSwinging && !reactedToSwing && !isAttacking && dist < attackRange + 1.5f)
        {
            reactedToSwing = true; // 1回の斬りにつき1回だけ判断する
            if (Random.value < guardChance) guardUntil = Time.time + 0.7f;
        }
        if (!playerSwinging) reactedToSwing = false;

        saber.SetGuard(Time.time < guardUntil);
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackStartTime = Time.time;

        // ため（セーバーを振りかぶって光らせる＝プレイヤーへの合図）
        saber.SetCharging(true);
        yield return new WaitForSeconds(windupTime);
        saber.SetCharging(false);

        // 斬る
        saber.Swing();
        while (saber.IsSwinging) yield return null;

        nextAttackTime = Time.time + attackInterval * Random.Range(0.7f, 1.3f);
        isAttacking = false;
    }

    // ため中に斬られたら怯んで攻撃がキャンセルされる
    void OnDamaged(int amount)
    {
        if (isAttacking && saber.IsCharging)
        {
            StopAllCoroutines();
            saber.SetCharging(false);
            isAttacking = false;
            nextAttackTime = Time.time + 1f;
        }
    }

    void OnDied()
    {
        StopAllCoroutines();
        saber.SetCharging(false);
        saber.SetGuard(false);
        enabled = false; // OnDisable が呼ばれ、All から外れる
    }
}
