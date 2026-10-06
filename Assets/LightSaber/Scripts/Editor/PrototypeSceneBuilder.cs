using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// メニュー「Light Saber > プロトタイプシーンを作成」で、
/// 遊べるテスト用シーンを自動で組み立てるエディタ拡張です。
/// （Editor フォルダに入れたスクリプトは、ゲーム本体ではなく Unity エディタの中だけで動きます）
/// </summary>
public static class PrototypeSceneBuilder
{
    const string ScenePath = BuilderUtil.Root + "/Scenes/Prototype.unity";

    [MenuItem("Light Saber/プロトタイプシーンを作成", priority = 0)]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("確認",
                $"{ScenePath} はすでにあります。作り直しますか？\n（設定したキャラクターモデルも元に戻ります）", "作り直す", "やめる"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuilderUtil.EnsureFolder(BuilderUtil.Root, "Scenes");

        // ---- マテリアル（色） ----
        Material playerMat = BuilderUtil.CreateMaterial("PlayerBody", new Color(0.85f, 0.82f, 0.75f), Color.black);
        Material enemyMat = BuilderUtil.CreateMaterial("EnemyBody", new Color(0.12f, 0.12f, 0.14f), Color.black, 0.3f, 0.7f);
        Material hiltMat = BuilderUtil.CreateMaterial("Hilt", new Color(0.6f, 0.62f, 0.66f), Color.black, 0.9f, 0.7f);
        Material blueBlade = BuilderUtil.CreateMaterial("BladeBlue", new Color(0.7f, 0.85f, 1f), new Color(0.2f, 0.5f, 1f) * 4f);
        Material redBlade = BuilderUtil.CreateMaterial("BladeRed", new Color(1f, 0.6f, 0.6f), new Color(1f, 0.1f, 0.1f) * 4f);

        // ---- カメラ ----
        Camera cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
        cam.gameObject.AddComponent<ThirdPersonCamera>();

        // ---- プレイヤー ----
        GameObject player = CreateCharacter("Player", new Vector3(0f, 1f, 0f), 0f,
            playerMat, hiltMat, blueBlade, new Color(0.3f, 0.6f, 1f), Health.TeamType.Player);
        player.tag = "Player";
        player.AddComponent<PlayerController>();
        BuilderUtil.Set(player.GetComponent<Health>(), "maxHp", p => p.intValue = 100);

        // ---- 敵 ----
        Vector3[] enemyPositions =
        {
            new Vector3(0f, 1f, 8f),
            new Vector3(-5f, 1f, 11f),
            new Vector3(5f, 1f, 11f),
        };
        for (int i = 0; i < enemyPositions.Length; i++)
        {
            GameObject enemy = CreateCharacter($"Enemy_{i + 1}", enemyPositions[i], 180f,
                enemyMat, hiltMat, redBlade, new Color(1f, 0.2f, 0.2f), Health.TeamType.Enemy);
            enemy.AddComponent<EnemyAI>();
            Health h = enemy.GetComponent<Health>();
            BuilderUtil.Set(h, "maxHp", p => p.intValue = 60);
            BuilderUtil.Set(h, "destroyDelay", p => p.floatValue = 3f);
            BuilderUtil.Set(enemy.GetComponentInChildren<Lightsaber>(), "damage", p => p.intValue = 10);
        }

        // ---- 地面・建物・ライト・霧など ----
        EnvironmentBuilder.Build();

        // ---- ゲーム管理 ----
        new GameObject("GameManager").AddComponent<GameManager>();

        // ---- 保存 ----
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;

        EditorUtility.DisplayDialog("完成！",
            "プロトタイプシーンを作成しました。\n画面上の ▶（再生）ボタンを押して遊んでみましょう！", "OK");
    }

    /// <summary>カプセルの体＋ライトセーバーを持ったキャラクターを作る</summary>
    static GameObject CreateCharacter(string name, Vector3 position, float yRotation,
        Material bodyMat, Material hiltMat, Material bladeMat, Color lightColor, Health.TeamType team)
    {
        // 体
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name;
        body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yRotation, 0f));
        Object.DestroyImmediate(body.GetComponent<Collider>()); // CharacterController を使うので不要
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;

        CharacterController cc = body.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

        // 顔の向きがわかるようにバイザーを付ける（人型モデルに差し替えると消える）
        BuilderUtil.CreatePart(PrimitiveType.Cube, "Visor", body.transform,
            new Vector3(0f, 0.5f, 0.4f), new Vector3(0.6f, 0.15f, 0.25f), hiltMat);

        // ライトセーバー（手首）
        Transform pivot = new GameObject("Lightsaber").transform;
        pivot.SetParent(body.transform, false);
        pivot.localPosition = new Vector3(0.45f, 0.1f, 0.35f);

        BuilderUtil.CreatePart(PrimitiveType.Cylinder, "Hilt", pivot,
            Vector3.zero, new Vector3(0.07f, 0.13f, 0.07f), hiltMat);

        Transform bladeRoot = new GameObject("BladeRoot").transform;
        bladeRoot.SetParent(pivot, false);
        bladeRoot.localPosition = new Vector3(0f, 0.13f, 0f);

        BuilderUtil.CreatePart(PrimitiveType.Cylinder, "Blade", bladeRoot,
            new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 0.6f, 0.05f), bladeMat);

        GameObject lightObj = new GameObject("BladeLight");
        lightObj.transform.SetParent(bladeRoot, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        Light bladeLight = lightObj.AddComponent<Light>();
        bladeLight.type = LightType.Point;
        bladeLight.color = lightColor;
        bladeLight.range = 3f;
        bladeLight.intensity = 2f;

        Lightsaber saber = pivot.gameObject.AddComponent<Lightsaber>();
        BuilderUtil.Set(saber, "bladeRoot", p => p.objectReferenceValue = bladeRoot);
        BuilderUtil.Set(saber, "bladeLight", p => p.objectReferenceValue = bladeLight);

        Health health = body.AddComponent<Health>();
        BuilderUtil.Set(health, "team", p => p.enumValueIndex = (int)team);
        BuilderUtil.Set(health, "bodyRenderer", p => p.objectReferenceValue = body.GetComponent<Renderer>());

        return body;
    }

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
