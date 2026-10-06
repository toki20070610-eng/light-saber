using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// メニュー「Light Saber > キャラクターモデルを設定」で開くウィンドウ。
/// Mixamo などの人型モデル（FBX）と、待機・走りのアニメーションを指定すると、
/// カプセルの見た目を人型モデルに差し替えます。
///
/// 自動でやってくれること：
///  1. FBX の設定を「Humanoid（人型）」にする
///  2. 待機・走りアニメを「ループ再生」にする
///  3. 止まる⇔走るを切り替える Animator Controller を作る
///  4. カプセルを消して、人型モデルを置く
///  5. 手がライトセーバーを握るように IK を設定する
/// </summary>
public class CharacterModelSetupWindow : EditorWindow
{
    static readonly string[] TargetLabels = { "プレイヤー", "敵（シーンにいる全員）" };

    int target;
    GameObject model;
    Object idle;
    Object run;
    Vector2 scroll;

    [MenuItem("Light Saber/キャラクターモデルを設定", priority = 20)]
    static void Open()
    {
        var window = GetWindow<CharacterModelSetupWindow>("キャラクターモデル設定");
        window.minSize = new Vector2(380, 320);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Mixamo などからダウンロードした FBX ファイルを、Project ウィンドウからドラッグして入れてください。\n" +
            "・キャラクターモデル … 「With Skin」でダウンロードしたもの\n" +
            "・待機 / 走り … アニメーションの FBX（走りは「In Place」にチェックしたもの）",
            MessageType.Info);
        EditorGUILayout.Space();

        target = EditorGUILayout.Popup("設定する相手", target, TargetLabels);
        model = (GameObject)EditorGUILayout.ObjectField("キャラクターモデル", model, typeof(GameObject), false);
        idle = EditorGUILayout.ObjectField("待機アニメ（Idle）", idle, typeof(Object), false);
        run = EditorGUILayout.ObjectField("走りアニメ（Run）", run, typeof(Object), false);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(model == null || idle == null || run == null))
        {
            if (GUILayout.Button("セットアップ！", GUILayout.Height(36))) Apply();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "先に「プロトタイプシーンを作成」でシーンを開いておいてください。\n" +
            "終わったら Ctrl + S（Mac は Cmd + S）でシーンを保存しましょう。",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    void Apply()
    {
        // ---- 対象のキャラクターを探す ----
        List<GameObject> characters = FindTargets();
        if (characters.Count == 0)
        {
            EditorUtility.DisplayDialog("見つかりません",
                "シーンに対象のキャラクターがいません。\n先に「Light Saber > プロトタイプシーンを作成」を実行してください。", "OK");
            return;
        }

        // ---- FBX の設定を整える ----
        string modelPath = AssetDatabase.GetAssetPath(model);
        string idlePath = AssetDatabase.GetAssetPath(idle);
        string runPath = AssetDatabase.GetAssetPath(run);
        string idleName = idle is AnimationClip ic ? ic.name : null;
        string runName = run is AnimationClip rc ? rc.name : null;

        PrepareModelImporter(modelPath, loop: false);
        ExtractEmbeddedTextures(modelPath); // 色（テクスチャ）を取り出す
        PrepareModelImporter(idlePath, loop: true);
        PrepareModelImporter(runPath, loop: true);

        // 設定を変えると読み込み直しになるので、ファイルからもう一度取り出す
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        AnimationClip idleClip = FindClip(idlePath, idleName);
        AnimationClip runClip = FindClip(runPath, runName);

        if (modelAsset == null || idleClip == null || runClip == null)
        {
            EditorUtility.DisplayDialog("エラー",
                "モデルかアニメーションを読み込めませんでした。\nFBX ファイルを入れたか確認してください。", "OK");
            return;
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isHuman)
        {
            EditorUtility.DisplayDialog("エラー",
                "このモデルは人型（Humanoid）として設定できませんでした。\n" +
                "Mixamo の人型キャラクターなど、人の形をしたモデルを使ってください。", "OK");
            return;
        }

        // ---- Animator Controller を作る ----
        string controllerName = target == 0 ? "Player" : "Enemy";
        AnimatorController controller = CreateController(controllerName, idleClip, runClip);

        // ---- キャラクターに付ける ----
        foreach (GameObject character in characters)
        {
            ApplyModel(character, modelAsset, avatar, controller);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("完成！",
            $"{characters.Count} 体のキャラクターをモデルに差し替えました。\n" +
            "Ctrl + S でシーンを保存してから ▶ で遊んでみましょう！", "OK");
    }

    /// <summary>Project ウィンドウで選んだ FBX のテクスチャを取り出して、色が付くようにする</summary>
    [MenuItem("Light Saber/選んだモデルの色を直す", priority = 21)]
    static void FixSelectedModelColors()
    {
        int fixedCount = 0;
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) && ExtractEmbeddedTextures(path))
            {
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            EditorUtility.DisplayDialog("完成！", $"{fixedCount} 個のモデルの色を直しました。", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("直せませんでした",
                "Project ウィンドウでキャラクターの FBX（With Skin でダウンロードしたもの）を選んでから実行してください。\n\n" +
                "それでもダメな場合は、FBX にテクスチャが入っていない可能性があります。\n" +
                "Mixamo で Format を「FBX for Unity (.fbx)」にしてダウンロードし直してください。\n" +
                "（X Bot / Y Bot など、もともと単色のキャラもいます）", "OK");
        }
    }

    /// <summary>
    /// FBX の中に埋め込まれたテクスチャ（画像）を取り出す。
    /// Unity は埋め込まれたままだと色を読めないことがあるため。
    /// </summary>
    static bool ExtractEmbeddedTextures(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) return false;

        string dir = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string folderName = System.IO.Path.GetFileNameWithoutExtension(modelPath) + "_Textures";
        string folder = $"{dir}/{folderName}";
        bool createdFolder = false;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(dir, folderName);
            createdFolder = true;
        }

        bool extracted = importer.ExtractTextures(folder);
        if (extracted)
        {
            AssetDatabase.Refresh();
            importer.SaveAndReimport(); // 取り出したテクスチャをマテリアルにつなぎ直す
        }
        else if (createdFolder)
        {
            AssetDatabase.DeleteAsset(folder); // 何も取り出せなかったら空フォルダを消す
        }
        return extracted;
    }

    List<GameObject> FindTargets()
    {
        var list = new List<GameObject>();
        if (target == 0)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) list.Add(player);
        }
        else
        {
            foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)) list.Add(enemy.gameObject);
        }
        return list;
    }

    /// <summary>FBX を人型（Humanoid）にし、必要ならアニメをループにする</summary>
    static void PrepareModelImporter(string path, bool loop)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return; // FBX ではない（.anim ファイルなど）ならそのまま使う

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        if (!loop) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            // その場で再生する（キャラの移動はスクリプトが担当するため）
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static AnimationClip FindClip(string path, string preferredName)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();
        if (preferredName != null)
        {
            AnimationClip named = clips.FirstOrDefault(c => c.name == preferredName);
            if (named != null) return named;
        }
        return clips.FirstOrDefault();
    }

    /// <summary>速さ（Speed）に応じて 待機 ⇔ 走り を混ぜる Animator Controller を作る</summary>
    static AnimatorController CreateController(string name, AnimationClip idleClip, AnimationClip runClip)
    {
        BuilderUtil.EnsureFolder(BuilderUtil.Root, "Animations");
        string path = $"{BuilderUtil.Root}/Animations/{name}.controller";
        AssetDatabase.DeleteAsset(path); // 作り直す

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idleClip, 0f);
        tree.AddChild(runClip, 5f);

        // IK Pass をオンにする（SaberHandIK が手を動かせるようになる）
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        AssetDatabase.SaveAssets();
        return controller;
    }

    static void ApplyModel(GameObject character, GameObject modelAsset, Avatar avatar, AnimatorController controller)
    {
        // ---- 古い見た目を消す ----
        Transform oldModel = character.transform.Find("Model");
        if (oldModel != null) DestroyImmediate(oldModel.gameObject);
        Transform visor = character.transform.Find("Visor");
        if (visor != null) DestroyImmediate(visor.gameObject);
        MeshRenderer capsuleRenderer = character.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) DestroyImmediate(capsuleRenderer);
        MeshFilter capsuleFilter = character.GetComponent<MeshFilter>();
        if (capsuleFilter != null) DestroyImmediate(capsuleFilter);

        // ---- 人型モデルを置く（足が地面に付く高さに） ----
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, character.transform);
        model.name = "Model";
        CharacterController cc = character.GetComponent<CharacterController>();
        float footY = cc != null ? cc.center.y - cc.height * 0.5f - cc.skinWidth : -1f;
        model.transform.localPosition = new Vector3(0f, footY, 0f);
        model.transform.localRotation = Quaternion.identity;

        // ---- アニメーション ----
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // 移動はスクリプトが担当

        // ---- 手でセーバーを握る ----
        SaberHandIK ik = model.AddComponent<SaberHandIK>();
        Lightsaber saber = character.GetComponentInChildren<Lightsaber>();
        if (saber != null) BuilderUtil.Set(ik, "saberGrip", p => p.objectReferenceValue = saber.transform);

        if (character.GetComponent<CharacterAnimator>() == null) character.AddComponent<CharacterAnimator>();

        // ---- ダメージで光らせる部分をモデルに変更 ----
        Health health = character.GetComponent<Health>();
        Renderer body = model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (body == null) body = model.GetComponentInChildren<Renderer>();
        if (health != null) BuilderUtil.Set(health, "bodyRenderer", p => p.objectReferenceValue = body);
    }
}
