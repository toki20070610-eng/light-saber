using UnityEditor;
using UnityEngine;

/// <summary>
/// シーンを自動で組み立てるエディタ拡張で共通して使う便利関数。
/// </summary>
public static class BuilderUtil
{
    public const string Root = "Assets/LightSaber";
    public const string MaterialFolder = Root + "/Materials";

    static Shader defaultShader;

    /// <summary>
    /// 使っているレンダーパイプライン（URP / Built-in など）の標準シェーダー。
    /// 一時的に立方体を作り、付いてくる標準マテリアルから調べている。
    /// </summary>
    public static Shader DefaultShader
    {
        get
        {
            if (defaultShader == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                defaultShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
                Object.DestroyImmediate(temp);
            }
            return defaultShader;
        }
    }

    /// <summary>マテリアル（色・光り方・質感）を作る。同じ名前があれば上書きする。</summary>
    public static Material CreateMaterial(string name, Color color, Color emission,
        float metallic = 0f, float smoothness = 0.5f)
    {
        EnsureFolder(Root, "Materials");
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(DefaultShader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;

        // 金属っぽさ・ツヤ（URP と Built-in でプロパティ名が少し違う）
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{folderName}")) AssetDatabase.CreateFolder(parent, folderName);
    }

    /// <summary>立方体や円柱などを作って配置する。keepCollider = false なら見た目だけ（ぶつからない）。</summary>
    public static GameObject CreatePart(PrimitiveType type, string name, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider = false)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    /// <summary>private な [SerializeField] の値をエディタから設定するためのヘルパー</summary>
    public static void Set(Object target, string propertyName, System.Action<SerializedProperty> setter)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError($"{target.GetType().Name} に {propertyName} が見つかりません");
            return;
        }
        setter(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
