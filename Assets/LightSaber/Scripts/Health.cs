using System;
using System.Collections;
using UnityEngine;

/// <summary>攻撃が当たったときの結果</summary>
public enum HitResult
{
    Ignored,  // 無効（すでに倒れている等）
    Hit,      // 命中
    Blocked,  // ガードされた
}

/// <summary>
/// 体力（HP）を管理するクラス。プレイヤーにも敵にも付けます。
/// ダメージを受ける・ガードする・倒れる、をここで処理します。
/// </summary>
public class Health : MonoBehaviour
{
    public enum TeamType { Player, Enemy }

    [Tooltip("所属チーム。同じチーム同士では攻撃が当たらない")]
    [SerializeField] TeamType team = TeamType.Enemy;

    [Tooltip("最大HP")]
    [SerializeField] int maxHp = 100;

    [Tooltip("ガードが有効な正面の角度（度）。140なら正面から左右70度ずつ")]
    [SerializeField] float guardAngle = 140f;

    [Tooltip("ダメージを受けたときに光らせる体のRenderer")]
    [SerializeField] Renderer bodyRenderer;

    [Tooltip("倒れてから消えるまでの秒数。マイナスなら消えない")]
    [SerializeField] float destroyDelay = -1f;

    public TeamType Team => team;
    public int MaxHp => maxHp;
    public int CurrentHp { get; private set; }
    public bool IsDead => CurrentHp <= 0;

    // イベント：他のスクリプトが「ダメージを受けたら教えて」と登録できる仕組み
    // （C言語の関数ポインタのようなもの）
    public event Action<int> Damaged;
    public event Action Blocked;
    public event Action Died;

    Lightsaber saber;
    Color baseColor;

    void Awake()
    {
        CurrentHp = maxHp;
        saber = GetComponentInChildren<Lightsaber>();
        if (bodyRenderer != null) baseColor = bodyRenderer.material.color;
    }

    /// <summary>
    /// ダメージを与える。attackerPosition は攻撃してきた相手の位置（ガード判定に使う）。
    /// </summary>
    public HitResult TakeDamage(int amount, Vector3 attackerPosition)
    {
        if (IsDead) return HitResult.Ignored;

        // ガード中で、かつ相手が正面にいれば防ぐ
        if (saber != null && saber.IsGuarding && IsInFront(attackerPosition))
        {
            Blocked?.Invoke();
            return HitResult.Blocked;
        }

        CurrentHp = Mathf.Max(CurrentHp - amount, 0);
        Damaged?.Invoke(amount);
        if (bodyRenderer != null) StartCoroutine(FlashRoutine());

        if (CurrentHp == 0)
        {
            Died?.Invoke();
            StartCoroutine(DieRoutine());
        }
        return HitResult.Hit;
    }

    bool IsInFront(Vector3 position)
    {
        Vector3 toAttacker = position - transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f) return true;
        return Vector3.Angle(transform.forward, toAttacker) <= guardAngle * 0.5f;
    }

    // 一瞬赤く光らせる
    IEnumerator FlashRoutine()
    {
        bodyRenderer.material.color = new Color(1f, 0.3f, 0.3f);
        yield return new WaitForSecondsRealtime(0.1f);
        if (bodyRenderer != null) bodyRenderer.material.color = baseColor;
    }

    // 後ろに倒れる
    IEnumerator DieRoutine()
    {
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        if (saber != null) saber.SetOn(false);

        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(-90f, 0f, 0f);
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 0.5f;

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null; // 1フレーム待つ
        }
        transform.rotation = endRot;
        transform.position = endPos;

        if (destroyDelay >= 0f) Destroy(gameObject, destroyDelay);
    }
}
