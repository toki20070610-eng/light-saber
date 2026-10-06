using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ライトセーバー本体。プレイヤーと敵の両方が使います。
///
/// ・Swing()      … 斬る（呼ぶたびに右斬り→左斬りと交互になる＝コンボ）
/// ・SetGuard()   … ガードの構え
/// ・SetCharging()… 斬る前の「ため」（敵が攻撃の予兆を見せるのに使う）
/// ・Toggle()     … 刃の ON/OFF
///
/// このスクリプトは「手首」にあたるオブジェクトに付けます。
/// 刃はこのオブジェクトの上方向（ローカルY軸）に伸びている前提です。
/// 斬る動きは、このオブジェクトの角度をコードで回転させて作っています。
/// </summary>
public class Lightsaber : MonoBehaviour
{
    [Header("見た目の参照")]
    [Tooltip("刃の根元。ON/OFF時にこのオブジェクトのYスケールを伸び縮みさせる")]
    [SerializeField] Transform bladeRoot;
    [SerializeField] Light bladeLight;

    [Header("攻撃")]
    [SerializeField] int damage = 20;
    [Tooltip("1回の斬りにかかる秒数（小さいほど速い）")]
    [SerializeField] float swingDuration = 0.28f;
    [Tooltip("刃の長さ（当たり判定に使う）")]
    [SerializeField] float bladeLength = 1.3f;
    [Tooltip("当たり判定の太さ")]
    [SerializeField] float hitRadius = 0.3f;
    [Tooltip("この秒数以内に次の攻撃をするとコンボがつながる")]
    [SerializeField] float comboResetTime = 0.8f;

    [Header("構えの角度（X, Y, Z の回転角度）")]
    [SerializeField] Vector3 idleAngles = new Vector3(25f, 0f, -15f);
    [SerializeField] Vector3 guardAngles = new Vector3(15f, 0f, 75f);
    [SerializeField] Vector3 slashAStart = new Vector3(-20f, 0f, -70f); // 右上から
    [SerializeField] Vector3 slashAEnd = new Vector3(100f, 0f, 50f);    // 左下へ
    [SerializeField] Vector3 slashBStart = new Vector3(-20f, 0f, 70f);  // 左上から
    [SerializeField] Vector3 slashBEnd = new Vector3(100f, 0f, -50f);   // 右下へ

    [Header("演出")]
    [Tooltip("攻撃が当たった瞬間に時間をほぼ止める秒数（ヒットストップ）")]
    [SerializeField] float hitStopDuration = 0.06f;
    [Tooltip("刃が伸び縮みする速さ")]
    [SerializeField] float toggleSpeed = 6f;

    // 外から読める状態
    public bool IsOn { get; private set; } = true;
    public bool IsSwinging { get; private set; }
    public bool IsCharging { get; private set; }
    public bool IsGuarding => guardRequested && IsOn && !IsSwinging;

    Health owner;              // このセーバーの持ち主
    bool guardRequested;
    int comboIndex;
    float lastSwingEndTime = -999f;
    float baseLightIntensity;
    float flash;               // 弾かれたときに光を強くする量
    readonly HashSet<Health> hitThisSwing = new HashSet<Health>(); // 1回の斬りで同じ相手に2回当てないため
    Coroutine hitStopRoutine;

    void Awake()
    {
        owner = GetComponentInParent<Health>();
        if (bladeLight != null) baseLightIntensity = bladeLight.intensity;
        transform.localRotation = Quaternion.Euler(idleAngles);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        IsSwinging = false;
        IsCharging = false;
        if (hitStopRoutine != null)
        {
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }
    }

    // 毎フレーム呼ばれる
    void Update()
    {
        // ① 刃の伸び縮み
        if (bladeRoot != null)
        {
            Vector3 scale = bladeRoot.localScale;
            scale.y = Mathf.MoveTowards(scale.y, IsOn ? 1f : 0f, toggleSpeed * Time.deltaTime);
            bladeRoot.localScale = scale;
            bladeRoot.gameObject.SetActive(scale.y > 0.01f);
        }

        // ② 光の強さ（ため中・弾かれた直後は明るく）
        flash = Mathf.MoveTowards(flash, 0f, 6f * Time.deltaTime);
        if (bladeLight != null)
        {
            bladeLight.intensity = baseLightIntensity * (1f + flash + (IsCharging ? 1.5f : 0f));
        }

        // ③ 構え（斬っている最中はコルーチン側で角度を動かすので何もしない）
        if (!IsSwinging)
        {
            Vector3 target = idleAngles;
            if (IsCharging) target = NextSlashStart;
            else if (IsGuarding) target = guardAngles;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(target), 15f * Time.deltaTime);
        }
    }

    // ===== 外から呼ぶ操作 =====

    /// <summary>斬る。斬り始められたら true を返す。</summary>
    public bool Swing()
    {
        if (!IsOn || IsSwinging) return false;
        if (owner != null && owner.IsDead) return false;
        StartCoroutine(SwingRoutine());
        return true;
    }

    public void SetGuard(bool guard) => guardRequested = guard;

    public void SetCharging(bool charging)
    {
        if (charging) RefreshCombo();
        IsCharging = charging && IsOn && !IsSwinging;
    }

    public void Toggle() => SetOn(!IsOn);

    public void SetOn(bool on)
    {
        IsOn = on;
        if (!on)
        {
            guardRequested = false;
            IsCharging = false;
        }
    }

    // ===== 内部処理 =====

    Vector3 NextSlashStart => comboIndex % 2 == 0 ? slashAStart : slashBStart;

    void RefreshCombo()
    {
        if (Time.time - lastSwingEndTime > comboResetTime) comboIndex = 0;
    }

    /// <summary>
    /// コルーチン：何フレームにもまたがる処理を、上から順に書ける仕組み。
    /// 「yield return null;」で1フレーム待ってから続きを実行します。
    /// </summary>
    IEnumerator SwingRoutine()
    {
        IsSwinging = true;
        IsCharging = false;
        RefreshCombo();

        bool slashA = comboIndex % 2 == 0;
        comboIndex++;
        Vector3 start = slashA ? slashAStart : slashBStart;
        Vector3 end = slashA ? slashAEnd : slashBEnd;
        hitThisSwing.Clear();

        // (1) 振りかぶる：今の角度から開始角度へ素早く移動
        Quaternion from = transform.localRotation;
        Quaternion startRot = Quaternion.Euler(start);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.06f)
        {
            transform.localRotation = Quaternion.Slerp(from, startRot, t);
            yield return null;
        }

        // (2) 振り抜く：開始角度 → 終了角度。途中で当たり判定をする
        bool blocked = false;
        for (float t = 0f; t < 1f && !blocked; t += Time.deltaTime / swingDuration)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t); // 最初と最後をゆっくりにする
            transform.localRotation = Quaternion.Euler(Vector3.Lerp(start, end, eased));
            if (t > 0.1f && t < 0.9f) blocked = CheckHits();
            yield return null;
        }

        // (3) ガードで弾かれたら少し押し戻される
        if (blocked)
        {
            flash = 3f;
            Quaternion bounceFrom = transform.localRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
            {
                transform.localRotation = Quaternion.Slerp(bounceFrom, startRot, t * 0.5f);
                yield return null;
            }
        }

        IsSwinging = false;
        lastSwingEndTime = Time.time;
    }

    /// <summary>刃に沿って3か所に球を置き、触れた相手にダメージを与える。弾かれたら true。</summary>
    bool CheckHits()
    {
        if (!IsOn) return false;

        bool blocked = false;
        Vector3 attackerPos = owner != null ? owner.transform.position : transform.position;

        for (int i = 1; i <= 3; i++)
        {
            Vector3 point = transform.position + transform.up * (bladeLength * i / 3f);
            Collider[] hits = Physics.OverlapSphere(point, hitRadius, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider col in hits)
            {
                Health target = col.GetComponentInParent<Health>();
                if (target == null || target == owner || hitThisSwing.Contains(target)) continue;
                if (owner != null && target.Team == owner.Team) continue; // 味方には当たらない

                hitThisSwing.Add(target);
                HitResult result = target.TakeDamage(damage, attackerPos);
                if (result == HitResult.Blocked) blocked = true;
                if (result != HitResult.Ignored) DoHitStop();
            }
        }
        return blocked;
    }

    void DoHitStop()
    {
        if (hitStopDuration <= 0f) return;
        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0.05f; // 時間の流れを 5% に
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
        hitStopRoutine = null;
    }

    // シーンビューでこのオブジェクトを選ぶと、当たり判定の球が見える
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 1; i <= 3; i++)
        {
            Gizmos.DrawWireSphere(transform.position + transform.up * (bladeLength * i / 3f), hitRadius);
        }
    }
}
