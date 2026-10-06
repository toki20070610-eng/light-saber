using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム全体の進行を管理します。
/// ・HPバーや残り敵数の表示
/// ・勝ち負けの判定
/// ・Rキーでリトライ
///
/// 画面表示には、手軽に使える OnGUI という仕組みを使っています（試作用）。
/// 本格的な画面を作るときは Unity の UI（uGUI や UI Toolkit）に置き換えましょう。
/// </summary>
public class GameManager : MonoBehaviour
{
    enum State { Playing, Won, Lost }

    [Tooltip("プレイヤーのHealth（空なら Player タグから探す）")]
    [SerializeField] Health playerHealth;

    State state = State.Playing;
    int totalEnemies;

    void Start()
    {
        Time.timeScale = 1f;
        if (playerHealth == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<Health>();
        }
        if (playerHealth != null) playerHealth.Died += () => state = State.Lost;
        totalEnemies = EnemyAI.All.Count;
    }

    void Update()
    {
        if (state == State.Playing && totalEnemies > 0 && EnemyAI.All.Count == 0)
        {
            state = State.Won;
        }

        if (state != State.Playing && GameInput.RestartPressed)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name); // 同じシーンを読み直す
        }
    }

    void OnGUI()
    {
        // プレイヤーのHPバー
        if (playerHealth != null)
        {
            float rate = (float)playerHealth.CurrentHp / playerHealth.MaxHp;
            DrawBar(new Rect(20, 20, 300, 24), rate, new Color(0.3f, 0.7f, 1f),
                $"HP {playerHealth.CurrentHp} / {playerHealth.MaxHp}");
        }
        GUI.Label(new Rect(20, 50, 400, 24), $"残りの敵: {EnemyAI.All.Count} / {totalEnemies}");

        // 敵の頭上の小さなHPバー
        Camera cam = Camera.main;
        if (cam != null)
        {
            foreach (EnemyAI enemy in EnemyAI.All)
            {
                Vector3 screen = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * 1.4f);
                if (screen.z <= 0f) continue; // カメラの後ろ
                float rate = (float)enemy.Health.CurrentHp / enemy.Health.MaxHp;
                // OnGUI は画面の上が y=0 なので上下を反転する
                DrawBar(new Rect(screen.x - 30, Screen.height - screen.y, 60, 6), rate, new Color(1f, 0.3f, 0.3f), "");
            }
        }

        // 操作説明
        GUI.Label(new Rect(20, Screen.height - 80, 600, 70),
            "WASD: 移動 / Shift: ダッシュ / Space: ジャンプ\n" +
            "左クリック: 攻撃（連打でコンボ） / 右クリック長押し: ガード\n" +
            "F: セーバー ON/OFF / Esc: マウスカーソル解放");

        // 勝敗メッセージ
        if (state != State.Playing)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            string message = state == State.Won ? "銀河に平和が戻った！" : "ダークサイドに敗れた…";
            GUI.Label(new Rect(0, Screen.height / 2f - 60, Screen.width, 80), message, style);

            style.fontSize = 24;
            GUI.Label(new Rect(0, Screen.height / 2f + 20, Screen.width, 40), "R キーでリトライ", style);
        }
    }

    static void DrawBar(Rect rect, float rate, Color color, string text)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(rate), rect.height), Texture2D.whiteTexture);
        GUI.color = old;
        if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width, rect.height), text);
    }
}
