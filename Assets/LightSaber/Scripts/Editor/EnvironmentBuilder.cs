using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// メニュー「Light Saber > 建物・街を配置」で、
/// 宇宙の前哨基地っぽい街並みを自動で作るエディタ拡張です。
///
///   中央     … 戦闘エリア（光るライン付きの床）
///   その外側 … 隠れられる低い壁、木箱、街灯
///   さらに外 … 窓が光るビル
///   遠く     … 高層ビル群と星空、霧
///
/// 中身は全部「立方体・円柱・球」の組み合わせです。
/// 数値（大きさ・数・色）を変えれば、いろいろな街が作れます。
/// </summary>
public static class EnvironmentBuilder
{
    const string RootName = "Environment";

    // 素材
    static Material groundMat, arenaMat, lineMat, wallMat, crateMat, metalMat;
    static Material buildingMat, windowCyanMat, windowOrangeMat, lampMat, starMat;

    [MenuItem("Light Saber/建物・街を配置（今のシーンに追加）", priority = 1)]
    static void BuildFromMenu()
    {
        if (GameObject.Find(RootName) != null &&
            !EditorUtility.DisplayDialog("確認", "今ある街を消して作り直しますか？", "作り直す", "やめる"))
        {
            return;
        }
        Build();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("完成！", "街を配置しました。\nCtrl + S（Mac は Cmd + S）でシーンを保存してください。", "OK");
    }

    public static void Build()
    {
        RemoveOld();
        CreateMaterials();

        // 毎回同じ街になるように乱数の「種」を固定する（数字を変えると別の街になる）
        Random.InitState(2024);

        Transform root = new GameObject(RootName).transform;
        List<Vector3> keepClear = GetCharacterPositions(); // キャラの近くには物を置かない

        BuildGround(root);
        BuildArena(root);
        BuildCoverWalls(root);
        BuildCrates(root, keepClear);
        BuildLampPosts(root);
        BuildBuildings(root);
        BuildSkyline(root);
        BuildStars(root);
        SetupAtmosphere();
    }

    // ===== 準備 =====

    static void RemoveOld()
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            // 古い版で作った柱・地面も消す
            if (go.name == RootName || go.name == "Pillar" || go.name == "Ground") Object.DestroyImmediate(go);
        }
    }

    static void CreateMaterials()
    {
        groundMat = BuilderUtil.CreateMaterial("Ground", new Color(0.1f, 0.11f, 0.14f), Color.black, 0.3f, 0.75f);
        arenaMat = BuilderUtil.CreateMaterial("ArenaFloor", new Color(0.16f, 0.17f, 0.21f), Color.black, 0.5f, 0.85f);
        lineMat = BuilderUtil.CreateMaterial("ArenaLine", new Color(0.4f, 0.8f, 1f), new Color(0.2f, 0.6f, 1f) * 3f);
        wallMat = BuilderUtil.CreateMaterial("CoverWall", new Color(0.3f, 0.31f, 0.34f), Color.black, 0.2f, 0.4f);
        crateMat = BuilderUtil.CreateMaterial("Crate", new Color(0.35f, 0.3f, 0.22f), Color.black, 0.1f, 0.3f);
        metalMat = BuilderUtil.CreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f), Color.black, 0.9f, 0.6f);
        buildingMat = BuilderUtil.CreateMaterial("Building", new Color(0.13f, 0.14f, 0.18f), Color.black, 0.4f, 0.6f);
        windowCyanMat = BuilderUtil.CreateMaterial("WindowCyan", new Color(0.5f, 0.9f, 1f), new Color(0.2f, 0.7f, 1f) * 2f);
        windowOrangeMat = BuilderUtil.CreateMaterial("WindowOrange", new Color(1f, 0.7f, 0.4f), new Color(1f, 0.5f, 0.15f) * 2f);
        lampMat = BuilderUtil.CreateMaterial("Lamp", new Color(1f, 0.95f, 0.85f), new Color(1f, 0.9f, 0.7f) * 3f);
        starMat = BuilderUtil.CreateMaterial("Star", Color.white, Color.white * 2f);
    }

    static List<Vector3> GetCharacterPositions()
    {
        var list = new List<Vector3>();
        foreach (Health h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None)) list.Add(h.transform.position);
        return list;
    }

    // ===== 各パーツ =====

    static void BuildGround(Transform root)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root, false);
        ground.transform.localScale = new Vector3(12f, 1f, 12f); // 120m × 120m
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;
    }

    /// <summary>中央の戦闘エリア：少し明るい円形の床と、光るラインの輪</summary>
    static void BuildArena(Transform root)
    {
        Transform arena = new GameObject("Arena").transform;
        arena.SetParent(root, false);

        BuilderUtil.CreatePart(PrimitiveType.Cylinder, "Floor", arena,
            new Vector3(0f, 0.01f, 0f), new Vector3(22f, 0.01f, 22f), arenaMat);

        const int count = 32;
        const float radius = 10.5f;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count;
            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            GameObject line = BuilderUtil.CreatePart(PrimitiveType.Cube, "Line", arena,
                rot * new Vector3(0f, 0.03f, radius), new Vector3(1.4f, 0.02f, 0.12f), lineMat);
            line.transform.localRotation = rot;
        }
    }

    /// <summary>身を隠せる低い壁（ぐるっと一周、すき間あり）</summary>
    static void BuildCoverWalls(Transform root)
    {
        Transform parent = new GameObject("CoverWalls").transform;
        parent.SetParent(root, false);

        const int count = 8;
        const float radius = 14f;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count + 22.5f;
            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            Vector3 pos = rot * new Vector3(0f, 0.6f, radius);

            GameObject wall = BuilderUtil.CreatePart(PrimitiveType.Cube, "Wall", parent,
                pos, new Vector3(6f, 1.2f, 0.6f), wallMat, keepCollider: true);
            wall.transform.localRotation = rot;

            // 壁の上に光るライン
            GameObject trim = BuilderUtil.CreatePart(PrimitiveType.Cube, "Trim", wall.transform,
                new Vector3(0f, 0.52f, -0.52f), new Vector3(0.95f, 0.04f, 0.1f), lineMat);
            trim.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>木箱（コンテナ）をランダムに置く</summary>
    static void BuildCrates(Transform root, List<Vector3> keepClear)
    {
        Transform parent = new GameObject("Crates").transform;
        parent.SetParent(root, false);

        int placed = 0;
        for (int tries = 0; tries < 200 && placed < 12; tries++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(8.5f, 12.5f);
            Vector3 pos = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, radius);
            if (IsNear(pos, keepClear, 3f)) continue; // キャラの近くはやめておく

            float size = Random.Range(0.9f, 1.4f);
            pos.y = size * 0.5f;
            GameObject crate = BuilderUtil.CreatePart(PrimitiveType.Cube, "Crate", parent,
                pos, Vector3.one * size, crateMat, keepCollider: true);
            crate.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);

            // 金属の帯
            BuilderUtil.CreatePart(PrimitiveType.Cube, "Band", crate.transform,
                Vector3.zero, new Vector3(1.02f, 0.12f, 1.02f), metalMat);

            // たまに上にもう1つ積む
            if (Random.value < 0.3f)
            {
                GameObject top = BuilderUtil.CreatePart(PrimitiveType.Cube, "Crate", parent,
                    pos + Vector3.up * size * 0.9f, Vector3.one * size * 0.8f, crateMat, keepCollider: true);
                top.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
            }
            placed++;
        }
    }

    /// <summary>街灯（本物の光源つき）</summary>
    static void BuildLampPosts(Transform root)
    {
        Transform parent = new GameObject("LampPosts").transform;
        parent.SetParent(root, false);

        const int count = 6;
        const float radius = 16.5f;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count;
            Vector3 basePos = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, radius);

            Transform post = new GameObject("LampPost").transform;
            post.SetParent(parent, false);
            post.localPosition = basePos;

            BuilderUtil.CreatePart(PrimitiveType.Cylinder, "Pole", post,
                new Vector3(0f, 2.5f, 0f), new Vector3(0.18f, 2.5f, 0.18f), metalMat, keepCollider: true);
            BuilderUtil.CreatePart(PrimitiveType.Sphere, "Bulb", post,
                new Vector3(0f, 5.1f, 0f), Vector3.one * 0.5f, lampMat);

            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(post, false);
            light.transform.localPosition = new Vector3(0f, 4.8f, 0f);
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 4f;
            light.color = i % 2 == 0 ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.75f, 0.5f);
        }
    }

    /// <summary>窓が光るビル。中心の方を向くように並べる</summary>
    static void BuildBuildings(Transform root)
    {
        Transform parent = new GameObject("Buildings").transform;
        parent.SetParent(root, false);

        const int count = 12;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count + Random.Range(-8f, 8f);
            float radius = Random.Range(24f, 30f);
            float width = Random.Range(5f, 9f);
            float depth = Random.Range(5f, 8f);
            float height = Random.Range(7f, 18f);

            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            Transform building = new GameObject("Building").transform;
            building.SetParent(parent, false);
            building.localPosition = rot * new Vector3(0f, 0f, radius);
            building.localRotation = rot * Quaternion.Euler(0f, 180f, 0f); // 正面(+Z)を中心に向ける

            // 本体
            BuilderUtil.CreatePart(PrimitiveType.Cube, "Body", building,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), buildingMat, keepCollider: true);

            // 光る窓の帯（正面）
            Material windowMat = Random.value < 0.7f ? windowCyanMat : windowOrangeMat;
            for (float y = 2f; y < height - 1f; y += 2.2f)
            {
                if (Random.value < 0.2f) continue; // ところどころ消灯
                BuilderUtil.CreatePart(PrimitiveType.Cube, "Window", building,
                    new Vector3(0f, y, depth * 0.5f + 0.02f), new Vector3(width * 0.8f, 0.35f, 0.05f), windowMat);
            }

            // 屋上の設備
            BuilderUtil.CreatePart(PrimitiveType.Cube, "RoofUnit", building,
                new Vector3(Random.Range(-1f, 1f), height + 0.6f, Random.Range(-1f, 1f)),
                new Vector3(width * 0.4f, 1.2f, depth * 0.4f), metalMat);
            if (Random.value < 0.5f)
            {
                BuilderUtil.CreatePart(PrimitiveType.Cylinder, "Antenna", building,
                    new Vector3(width * 0.3f, height + 2.5f, 0f), new Vector3(0.1f, 2.5f, 0.1f), metalMat);
                BuilderUtil.CreatePart(PrimitiveType.Sphere, "AntennaLight", building,
                    new Vector3(width * 0.3f, height + 5f, 0f), Vector3.one * 0.3f, windowOrangeMat);
            }

            // 入口
            BuilderUtil.CreatePart(PrimitiveType.Cube, "Door", building,
                new Vector3(0f, 1.2f, depth * 0.5f + 0.02f), new Vector3(1.6f, 2.4f, 0.05f), lineMat);
        }
    }

    /// <summary>遠くの高層ビル群（背景）</summary>
    static void BuildSkyline(Transform root)
    {
        Transform parent = new GameObject("Skyline").transform;
        parent.SetParent(root, false);

        const int count = 28;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / count + Random.Range(-5f, 5f);
            float radius = Random.Range(42f, 55f);
            float width = Random.Range(6f, 12f);
            float height = Random.Range(20f, 60f);

            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            Transform tower = new GameObject("Tower").transform;
            tower.SetParent(parent, false);
            tower.localPosition = rot * new Vector3(0f, 0f, radius);
            tower.localRotation = rot * Quaternion.Euler(0f, 180f, 0f);

            BuilderUtil.CreatePart(PrimitiveType.Cube, "Body", tower,
                new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), buildingMat);

            // 縦に光るライン
            Material mat = Random.value < 0.75f ? windowCyanMat : windowOrangeMat;
            BuilderUtil.CreatePart(PrimitiveType.Cube, "Strip", tower,
                new Vector3(0f, height * 0.5f, width * 0.5f + 0.05f), new Vector3(0.3f, height * 0.9f, 0.1f), mat);
        }
    }

    /// <summary>空の星（遠くに小さな光る球を並べる）</summary>
    static void BuildStars(Transform root)
    {
        Transform parent = new GameObject("Stars").transform;
        parent.SetParent(root, false);

        for (int i = 0; i < 220; i++)
        {
            float yaw = Random.Range(0f, 360f);
            float pitch = Random.Range(8f, 85f); // 地平線より上だけ
            float distance = Random.Range(100f, 130f);
            Vector3 pos = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward * distance;
            BuilderUtil.CreatePart(PrimitiveType.Sphere, "Star", parent,
                pos, Vector3.one * Random.Range(0.3f, 0.9f), starMat);
        }
    }

    /// <summary>光・影・霧などの雰囲気</summary>
    static void SetupAtmosphere()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.32f);

        // 霧：遠くがかすんで奥行きが出る
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.08f);

        // 太陽（月明かり）：影を付ける
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.intensity = 0.6f;
            light.color = new Color(0.75f, 0.82f, 1f);
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
        }
    }

    static bool IsNear(Vector3 pos, List<Vector3> points, float distance)
    {
        foreach (Vector3 p in points)
        {
            Vector3 d = p - pos;
            d.y = 0f;
            if (d.magnitude < distance) return true;
        }
        return false;
    }
}
