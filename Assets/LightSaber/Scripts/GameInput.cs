using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// キーボード・マウスの入力をまとめたクラス。
/// 他のスクリプトは「GameInput.AttackPressed」のように書くだけで入力を読めます。
///
/// Unity には「旧 Input Manager」と「新 Input System」の2種類の入力方式があり、
/// プロジェクト設定によってどちらが使えるかが変わります。
/// #if ～ #else ～ #endif（C言語のプリプロセッサと同じ！）で、どちらの設定でも動くようにしています。
/// </summary>
public static class GameInput
{
#if ENABLE_LEGACY_INPUT_MANAGER
    // ===== 旧 Input Manager 版 =====

    /// <summary>移動入力（WASD / 矢印キー）。x = 左右, y = 前後</summary>
    public static Vector2 Move => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    /// <summary>マウスの移動量（カメラ回転用）</summary>
    public static Vector2 Look => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
    public static bool RunHeld => Input.GetKey(KeyCode.LeftShift);
    public static bool AttackPressed => Input.GetMouseButtonDown(0);   // 左クリック
    public static bool GuardHeld => Input.GetMouseButton(1);           // 右クリック長押し
    public static bool ToggleSaberPressed => Input.GetKeyDown(KeyCode.F);
    public static bool RestartPressed => Input.GetKeyDown(KeyCode.R);
    public static bool CancelPressed => Input.GetKeyDown(KeyCode.Escape);
#else
    // ===== 新 Input System 版 =====
    static Keyboard Kb => Keyboard.current;
    static Mouse Ms => Mouse.current;

    public static Vector2 Move
    {
        get
        {
            if (Kb == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (Kb.aKey.isPressed || Kb.leftArrowKey.isPressed) x -= 1f;
            if (Kb.dKey.isPressed || Kb.rightArrowKey.isPressed) x += 1f;
            if (Kb.sKey.isPressed || Kb.downArrowKey.isPressed) y -= 1f;
            if (Kb.wKey.isPressed || Kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
    }

    // 旧方式と同じくらいの感度になるよう 0.1 倍している
    public static Vector2 Look => Ms != null ? Ms.delta.ReadValue() * 0.1f : Vector2.zero;

    public static bool JumpPressed => Kb != null && Kb.spaceKey.wasPressedThisFrame;
    public static bool RunHeld => Kb != null && Kb.leftShiftKey.isPressed;
    public static bool AttackPressed => Ms != null && Ms.leftButton.wasPressedThisFrame;
    public static bool GuardHeld => Ms != null && Ms.rightButton.isPressed;
    public static bool ToggleSaberPressed => Kb != null && Kb.fKey.wasPressedThisFrame;
    public static bool RestartPressed => Kb != null && Kb.rKey.wasPressedThisFrame;
    public static bool CancelPressed => Kb != null && Kb.escapeKey.wasPressedThisFrame;
#endif
}
