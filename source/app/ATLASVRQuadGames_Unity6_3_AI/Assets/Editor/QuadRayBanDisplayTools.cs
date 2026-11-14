using System;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit authoring commands for the shared Quad assistant prefab.</summary>
public static class QuadRayBanDisplayTools
{
    public const string PrefabPath = "Assets/Prefabs/XR Assistant Rig.prefab";
    public const string ReportDirectory = "Diagnostics/2026-10-25-RayBanUI";
    private const string FontDirectory = "Assets/Art/RayBanDisplay/Fonts/";

    [MenuItem("ATLAS VR/Assistant/Install or rebuild display UI")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring the display.");
        var scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("QuadProjectAlexanderLau.unity", StringComparison.Ordinal))
            throw new InvalidOperationException("Open QuadProjectAlexanderLau before installing.");
        if (scene.isDirty) throw new InvalidOperationException("The open scene has unsaved work; preserve it before installing.");

        RepairImportedFontFallbacks();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var router = root.GetComponentInChildren<QuestDisplayUI>(true);
            if (router == null) throw new InvalidOperationException("Existing QuestDisplayUI is required.");
            var shell = GetOrAdd<RayBanDisplayShell>(router.gameObject);
            shell.router = router;
            router.visualShell = shell;
            shell.previewEnabled = shell.liveFeaturesEnabled = true;
            shell.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDirectory + "NotoSans-Regular SDF.asset");
            shell.samplePhotos = new[] { "gallery-coast", "gallery-forest", "gallery-overlook" }
                .Select(name => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/RayBanDisplay/Samples/" + name + ".png")).ToArray();
            shell.appSettings = GetOrAdd<RayBanAppSettings>(router.gameObject);
            shell.cameraBridge = GetOrAdd<RayBanCameraBridge>(router.gameObject);
            shell.cameraBridge.capture = Required<VRCameraCapture>(root);
            shell.musicBridge = GetOrAdd<RayBanMusicBridge>(router.gameObject);
            shell.musicBridge.player = Required<SimpleAudioPlayer>(root);
            shell.musicBridge.legacyPageRoot = router.transform.Find("Audio Player").gameObject;
            shell.assistantBridge = GetOrAdd<RayBanAssistantBridge>(router.gameObject);
            shell.assistantBridge.manager = Required<AIManager>(root);
            shell.assistantBridge.manager.centerEyeCamera = shell.cameraBridge.capture.snapshotCamera;
            shell.assistantBridge.serviceRoot = root.transform.Find("AI Behaviour").gameObject;
            shell.captionBridge = GetOrAdd<RayBanCaptionBridge>(router.gameObject);
            shell.captionBridge.manager = Required<LiveTranslationManager>(root);
            shell.captionBridge.legacyPageRoot = router.transform.Find("Captions Page").gameObject;
            shell.mapBridge = GetOrAdd<RayBanMapBridge>(router.gameObject);
            shell.mapBridge.navigator = Required<VRMapNavigator>(root);
            shell.servicesToSuspend = new[] { shell.assistantBridge.serviceRoot };
            // New Assistant explicitly acquires its services on opening the page.
            shell.assistantBridge.serviceRoot.SetActive(false);
            foreach (var entry in router.entries) if (entry != null && entry.page != null) entry.page.SetActive(false);
            if (router.mainMenuRoot != null) router.mainMenuRoot.SetActive(false);
            var input = GetOrAdd<QuadDisplayInput>(router.gameObject);
            input.router = router;
            input.shell = shell;
            input.enableControllerFocusNavigation = false;
            var twist = GetOrAdd<PinchAndTwistEventSource>(router.gameObject);
            shell.BuildVisualTree();
            AssignCaptionFonts(shell.FeatureScreens);
            var feature = new SerializedObject(shell.FeatureScreens);
            feature.FindProperty("mediaTwistSource").objectReferenceValue = twist;
            feature.ApplyModifiedPropertiesWithoutUndo();
            WirePointerSurface(shell.VisualRoot);
            SetLayer(shell.VisualRoot, LayerMask.NameToLayer("UI"));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // The player's existing hand and camera are scene references, so retain
        // them as instance overrides; never introduce a second player/rig.
        var instance = UnityEngine.Object.FindObjectsByType<QuestDisplayUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(item => item.gameObject.scene == scene);
        var rig = UnityEngine.Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(item => item.gameObject.scene == scene);
        var right = rig.GetComponentsInChildren<OVRHand>(true)
            .FirstOrDefault(hand => hand.name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0 || hand.transform.IsChildOf(rig.rightHandAnchor));
        // Some Quad hand prefabs are siblings of OVRCameraRig below the player.
        if (right == null)
            right = rig.transform.root.GetComponentsInChildren<OVRHand>(true)
                .FirstOrDefault(hand => hand.name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0 || PathOf(hand.transform).IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0);
        var sceneTwist = instance.GetComponent<PinchAndTwistEventSource>();
        sceneTwist.hand = right;
        sceneTwist.skeleton = right != null ? right.GetComponent<OVRSkeleton>() : null;
        PrefabUtility.RecordPrefabInstancePropertyModifications(sceneTwist);
        var canvas = instance.visualShell.VisualRoot.GetComponent<Canvas>();
        canvas.worldCamera = rig.centerEyeAnchor.GetComponent<Camera>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
        var assistant = new SerializedObject(instance.visualShell.assistantBridge.manager);
        var visionCamera = assistant.FindProperty("centerEyeCamera");
        if (visionCamera.prefabOverride && visionCamera.objectReferenceValue == null)
            PrefabUtility.RevertPropertyOverride(visionCamera, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Validate();
    }

    private static void WirePointerSurface(RectTransform visual)
    {
        var canvas = visual.GetComponent<Canvas>();
        var pointable = GetOrAdd<PointableCanvas>(visual.gameObject);
        pointable.InjectAllPointableCanvas(canvas);
        var plane = GetOrAdd<PlaneSurface>(visual.gameObject);
        plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward, false);
        var bounds = GetOrAdd<BoundsClipper>(visual.gameObject);
        bounds.Position = Vector3.zero;
        bounds.Size = new Vector3(600, 600, 1);
        var clipped = GetOrAdd<ClippedPlaneSurface>(visual.gameObject);
        clipped.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { bounds });
        var ray = GetOrAdd<RayInteractable>(visual.gameObject);
        ray.InjectAllRayInteractable(clipped);
        ray.InjectOptionalPointableElement(pointable);
        var poke = GetOrAdd<PokeInteractable>(visual.gameObject);
        poke.InjectAllPokeInteractable(clipped);
        poke.InjectOptionalPointableElement(pointable);
    }

    private static void RepairImportedFontFallbacks()
    {
        // The imported Latin atlases need explicit Korean, Chinese and Japanese
        // fallbacks. Keep source fonts embedded for runtime dynamic glyph creation.
        var korean = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Scripts/XR Assistant/NANUMGOTHIC SDF.asset");
        if (korean == null || korean.sourceFontFile == null)
            throw new InvalidOperationException("Missing existing Korean font or its dynamic source font.");
        korean.isMultiAtlasTexturesEnabled = true;
        EditorUtility.SetDirty(korean);
        var chinese = EnsureCaptionFont("NotoSansSC-Regular", korean);
        var japanese = EnsureCaptionFont("NotoSansJP-Regular", korean);
        var fallbacks = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/TextMesh Pro" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).Where(font => font != null).ToList();
        fallbacks.Insert(0, korean);
        fallbacks.Insert(1, chinese);
        fallbacks.Insert(2, japanese);
        foreach (var name in new[] { "NotoSans-Regular SDF.asset", "NotoSans-Bold SDF.asset" })
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDirectory + name);
            if (font == null) throw new InvalidOperationException("Missing imported display font: " + name);
            font.fallbackFontAssetTable = fallbacks;
            EditorUtility.SetDirty(font);
        }
        // Locale variants reuse the Latin atlas, but prioritize the appropriate
        // Han glyph forms. Both retain all language fallbacks for mixed history.
        EnsureCaptionTypeface("NotoSans-Captions-Chinese", chinese, japanese, korean, fallbacks);
        EnsureCaptionTypeface("NotoSans-Captions-Japanese", japanese, chinese, korean, fallbacks);
    }

    private static void EnsureCaptionTypeface(string name, TMP_FontAsset primary, TMP_FontAsset secondary,
        TMP_FontAsset korean, System.Collections.Generic.List<TMP_FontAsset> fallbacks)
    {
        var latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDirectory + "NotoSans-Regular SDF.asset");
        string path = FontDirectory + name + " SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null)
        {
            font = UnityEngine.Object.Instantiate(latin);
            font.name = name + " SDF";
            AssetDatabase.CreateAsset(font, path);
        }
        font.fallbackFontAssetTable = new[] { primary, secondary, korean }.Concat(fallbacks).Distinct().ToList();
        font.ReadFontAssetDefinition();
        EditorUtility.SetDirty(font);
    }

    private static TMP_FontAsset EnsureCaptionFont(string name, TMP_FontAsset korean)
    {
        string path = FontDirectory + name + " SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontDirectory + name + ".otf");
            if (source == null) throw new InvalidOperationException("Missing caption font source: " + name);
            font = TMP_FontAsset.CreateFontAsset(source, 60, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = name + " SDF";
            // Allocate an initial atlas without baking the entire CJK character set.
            font.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?…。、！？");
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        }
        if (font.sourceFontFile == null) throw new InvalidOperationException("Missing dynamic source font: " + name);
        font.isMultiAtlasTexturesEnabled = true;
        if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(korean)) font.fallbackFontAssetTable.Add(korean);
        EditorUtility.SetDirty(font);
        return font;
    }

    private static void AssignCaptionFonts(RayBanFeatureScreen screen)
    {
        var serialized = new SerializedObject(screen);
        serialized.FindProperty("captionChineseFont").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDirectory + "NotoSans-Captions-Chinese SDF.asset");
        serialized.FindProperty("captionJapaneseFont").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDirectory + "NotoSans-Captions-Japanese SDF.asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("ATLAS VR/Assistant/Repair caption fonts")]
    public static void RepairCaptionFonts()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before saving caption fonts.");
        RepairImportedFontFallbacks();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AssignCaptionFonts(Required<RayBanFeatureScreen>(root));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("ATLAS VR/Assistant/Validate display UI")]
    public static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(item => item.gameObject.scene == scene);
        var visual = shell.VisualRoot;
        var twist = shell.GetComponent<PinchAndTwistEventSource>();
        var report = new
        {
            project = Application.dataPath, scene = scene.path, sceneSaved = !scene.isDirty,
            shell = PathOf(shell.transform), liveFeatures = shell.liveFeaturesEnabled,
            launcherApps = visual.GetComponentsInChildren<RayBanTile>(true).Count(tile => tile.PageId == 2),
            pageTabs = visual.GetComponentsInChildren<RayBanPageTab>(true).Length,
            pointableCanvas = visual.GetComponent<PointableCanvas>() != null,
            ray = visual.GetComponent<RayInteractable>() != null, poke = visual.GetComponent<PokeInteractable>() != null,
            font = AssetDatabase.GetAssetPath(shell.font),
            camera = shell.cameraBridge.capture != null, music = shell.musicBridge.player != null,
            assistant = shell.assistantBridge.manager != null, captions = shell.captionBridge.manager != null,
            maps = shell.mapBridge.navigator != null,
            assistantVisionCamera = shell.assistantBridge.manager.centerEyeCamera != null
                ? PathOf(shell.assistantBridge.manager.centerEyeCamera.transform) : null,
            assistantUsesGameplayCamera = shell.assistantBridge.manager.centerEyeCamera != null &&
                shell.assistantBridge.manager.centerEyeCamera == shell.cameraBridge.capture.snapshotCamera,
            rightHand = twist.hand != null ? PathOf(twist.hand.transform) : null,
            skeleton = twist.skeleton != null,
            rigCount = UnityEngine.Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(item => item.gameObject.scene == scene),
            eventSystems = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene == scene).Select(item => new { path = PathOf(item.transform), enabled = item.enabled, active = item.gameObject.activeInHierarchy }).ToArray(),
            missingScriptsInAssistant = shell.transform.root.GetComponentsInChildren<Transform>(true).Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject))
        };
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(Path.Combine(ReportDirectory, "authoring-validation.json"), Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("[QuadDisplay] Authored display validation saved to " + ReportDirectory + "/authoring-validation.json");
    }

    private static T Required<T>(GameObject root) where T : Component => root.GetComponentsInChildren<T>(true).Single();
    private static T GetOrAdd<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
    private static string PathOf(Transform node) => node.parent != null ? PathOf(node.parent) + "/" + node.name : node.name;
    private static void SetLayer(Transform node, int layer)
    {
        if (layer >= 0) node.gameObject.layer = layer;
        foreach (Transform child in node) SetLayer(child, layer);
    }
}
