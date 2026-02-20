using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Editor utility: creates a static TMP Font Asset from PingFang.ttc with all
/// glyphs pre-baked from every dialogue file (equivalent to Font Asset Creator).
/// Assigns the font to all TMP components in EVERY game scene and saves each scene.
/// Run via Tools > Setup CJK Font.
/// </summary>
public class CJKFontSetup
{
    private const string FontPath    = "Assets/Fonts/PingFang.ttc";
    private const string AssetPath   = "Assets/Fonts/PingFang_TMP.asset";
    private const string WenKaiFontPath  = "Assets/Fonts/LXGWWenKaiTC-Medium.ttf";
    private const string WenKaiAssetPath = "Assets/Fonts/LXGWWenKaiTC_TMP.asset";
    private const string DialogueDir = "Assets/Dialogue";

    // All game scenes that contain TMP text — add new scenes here when they are created
    private static readonly string[] GameScenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Angel.unity",
    };

    [MenuItem("Tools/Setup CJK Font")]
    public static void SetupFont()
    {
        // 1. Load source font
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"Font not found at {FontPath}.");
            return;
        }

        // 2. Collect every character that will ever appear in the game
        string characters = CollectCharacters();
        Debug.Log($"Building static font atlas for {characters.Length} unique characters.");

        // 3. Build font asset — delete old only if it exists (GUID will change, handled below)
        AssetDatabase.DeleteAsset(AssetPath);

        TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA_HINTED,
            atlasWidth: 2048,
            atlasHeight: 2048,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (tmpFont == null) { Debug.LogError("Failed to create TMP Font Asset."); return; }

        // 4. Pre-rasterize every character into the atlas (same as Font Asset Creator)
        tmpFont.TryAddCharacters(characters, out string missing);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning($"Glyphs not found in font: [{missing}]");

        // 5. Save font asset + embed atlas textures as sub-assets
        AssetDatabase.CreateAsset(tmpFont, AssetPath);
        foreach (Texture2D atlasTex in tmpFont.atlasTextures)
        {
            if (atlasTex == null) continue;
            atlasTex.name = tmpFont.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlasTex, tmpFont);
        }

        // 6. Switch to Static so the atlas is never cleared at domain reload or build
        var so = new SerializedObject(tmpFont);
        so.FindProperty("m_AtlasPopulationMode").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tmpFont);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 7. Build WenKai TMP font for title text (Static — pre-baked title characters)
        TMP_FontAsset wenKaiFont = null;
        Font wenKaiSource = AssetDatabase.LoadAssetAtPath<Font>(WenKaiFontPath);
        if (wenKaiSource != null)
        {
            AssetDatabase.DeleteAsset(WenKaiAssetPath);
            wenKaiFont = TMP_FontAsset.CreateFontAsset(
                wenKaiSource,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA_HINTED,
                atlasWidth: 2048,
                atlasHeight: 2048,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (wenKaiFont != null)
            {
                // Pre-bake ALL characters into atlas (same set as PingFang)
                wenKaiFont.TryAddCharacters(characters, out string wenKaiMissing);
                if (!string.IsNullOrEmpty(wenKaiMissing))
                    Debug.LogWarning($"WenKai missing glyphs: [{wenKaiMissing}]");

                // Save font asset + embed atlas textures + material as sub-assets
                AssetDatabase.CreateAsset(wenKaiFont, WenKaiAssetPath);
                foreach (Texture2D atlasTex in wenKaiFont.atlasTextures)
                {
                    if (atlasTex == null) continue;
                    atlasTex.name = wenKaiFont.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlasTex, wenKaiFont);
                }
                // Material must also be saved as sub-asset (TMP creates it in memory)
                if (wenKaiFont.material != null)
                {
                    wenKaiFont.material.name = wenKaiFont.name + " Material";
                    AssetDatabase.AddObjectToAsset(wenKaiFont.material, wenKaiFont);
                }

                // Switch to Static so atlas survives domain reload (same as PingFang)
                var wenKaiSO = new SerializedObject(wenKaiFont);
                wenKaiSO.FindProperty("m_AtlasPopulationMode").intValue = 0;
                wenKaiSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(wenKaiFont);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(WenKaiAssetPath, ImportAssetOptions.ForceUpdate);
                // Reload the asset after reimport so all references are fresh
                wenKaiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(WenKaiAssetPath);
                Debug.Log($"WenKai TC TMP font asset created (Static) — {characters.Length} characters pre-baked.");
            }
        }
        else
        {
            Debug.LogWarning($"WenKai font not found at {WenKaiFontPath}, skipping title font.");
        }

        // 8. Assign font to ALL game scenes (open each additively, assign, save, close)
        Scene activeScene = EditorSceneManager.GetActiveScene();
        int totalAssigned = 0;

        foreach (string scenePath in GameScenes)
        {
            bool isAlreadyOpen = activeScene.path == scenePath;
            Scene scene = isAlreadyOpen
                ? activeScene
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            var tmpComponents = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            int sceneCount = 0;
            foreach (TextMeshProUGUI tmp in tmpComponents)
            {
                // Only process objects from this specific scene
                if (tmp.gameObject.scene.path != scenePath) continue;
                Undo.RecordObject(tmp, "Assign CJK Font");
                tmp.font = tmpFont;
                EditorUtility.SetDirty(tmp);
                sceneCount++;
            }

            // Assign WenKai title font to DistrictMapManager in MainMenu scene
            if (wenKaiFont != null)
            {
                var managers = Object.FindObjectsByType<DistrictMapManager>(FindObjectsSortMode.None);
                foreach (DistrictMapManager mgr in managers)
                {
                    if (mgr.gameObject.scene.path != scenePath) continue;
                    Undo.RecordObject(mgr, "Assign Title Font");
                    mgr.titleFont = wenKaiFont;
                    EditorUtility.SetDirty(mgr);
                }
            }

            EditorSceneManager.SaveScene(scene);
            totalAssigned += sceneCount;
            Debug.Log($"  {System.IO.Path.GetFileNameWithoutExtension(scenePath)}: {sceneCount} TMP components assigned.");

            if (!isAlreadyOpen)
                EditorSceneManager.CloseScene(scene, true);
        }

        // 9. Assign font to prefab assets (not found by FindObjectsByType)
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        int prefabCount = 0;
        foreach (string guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null) continue;
            var labels = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (labels.Length == 0) continue;
            foreach (TextMeshProUGUI label in labels)
            {
                label.font = tmpFont;
                EditorUtility.SetDirty(label);
                prefabCount++;
            }
        }
        if (prefabCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"  Prefabs: {prefabCount} TMP components assigned.");
        }

        Debug.Log($"CJK Font Setup done — static atlas, {totalAssigned} total TMP components assigned across all scenes.");
    }

    static string CollectCharacters()
    {
        var chars = new HashSet<char>();

        // Full ASCII printable range
        for (char c = ' '; c <= '~'; c++) chars.Add(c);

        // UI symbols used in hints / end screen
        foreach (char c in "—▼…~()") chars.Add(c);

        // Hard-coded menu / UI strings
        foreach (char c in "美麗新香港中西區東南灣仔九龍城觀塘深水埗黃大仙油尖旺離島葵青北西貢沙田大埔荃屯門元朗放假選單返回選擇場景第一集章即將推出關閉開始遊戲點擊任意位置繼續確認取消外國人") chars.Add(c);

        // All characters from every .txt file in the Dialogue folder
        string dialogueDir = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", DialogueDir));

        if (Directory.Exists(dialogueDir))
        {
            foreach (string filePath in Directory.GetFiles(dialogueDir, "*.txt"))
            {
                foreach (string raw in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int sep = line.IndexOf('|');
                    if (sep < 0) continue;
                    foreach (char c in line) chars.Add(c);
                }
            }
            chars.Remove('|');
        }
        else
        {
            Debug.LogWarning($"Dialogue directory not found at {dialogueDir}. Atlas will contain ASCII only.");
        }

        var sorted = new List<char>(chars);
        sorted.Sort();
        return new string(sorted.ToArray());
    }
}
