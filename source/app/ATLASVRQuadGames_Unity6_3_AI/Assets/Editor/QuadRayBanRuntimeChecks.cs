#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Nonblocking Editor checks. Does not activate recognition, translation, or AI requests.</summary>
[InitializeOnLoad]
public static class QuadRayBanRuntimeChecks
{
    private const string Key = "QuadRayBanRuntimeChecks.";
    private const string OutputPath = "Diagnostics/2026-11-01-QuestValidation/inapp-runtime-checks.json";
    private const string ActivityTempPrefix = "QuadRuntimeActivityChecks-";
    private static IDisposable activityOverride;
    private static QuadLocalNotificationStore originalActivityStore, testActivityStore;
    private static string activityTempDirectory;
    [Serializable] private class CheckResult { public string name; public bool passed; public string detail; }
    [Serializable] private class Report
    {
        public string startedUtc, finishedUtc, status, scene;
        public bool startedPlayMode, manuallyRenderedCamera;
        public int passed, failed;
        public List<CheckResult> checks = new List<CheckResult>();
        public List<string> limitations = new List<string>();
    }

    static QuadRayBanRuntimeChecks()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    }

    [MenuItem("Tools/Ray-Ban Display/Validate Quad runtime integration")]
    public static void StartChecks()
    {
        if (SessionState.GetBool(Key + "active", false)) throw new InvalidOperationException("Quad display validation is already running.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Wait for Unity import and compilation before validation.");
        var current = FindShell();
        if (current == null || current.VisualRoot == null)
            throw new InvalidOperationException("Author the Quad Ray-Ban shell in the intended scene before validation.");
        if (EditorApplication.isPlaying && ((current.assistantBridge != null && (current.assistantBridge.IsBusy || current.assistantBridge.IsListening)) ||
            (current.captionBridge != null && (current.captionBridge.IsListening || current.captionBridge.IsBusy))))
            throw new InvalidOperationException("Close the active voice session before running checks; validation will not interrupt it.");
        bool startPlay = !EditorApplication.isPlaying;
        var report = new Report
        {
            startedUtc = DateTime.UtcNow.ToString("O"), status = "running", scene = current.gameObject.scene.path,
            startedPlayMode = startPlay,
            limitations = new List<string>
            {
                "Editor lifecycle and rendered gameplay camera checks; no physical Quest or optical gesture verification.",
                "No Listen, caption Start, dictation, Gemini, or translation request is issued. SDK initialization may still run.",
                "Music is muted during playback checks. Cloud authorization, microphone input, spoken audio, and Android builds are not tested.",
                "Maps checks cover UI/backend integration; waypoint geometry, physical traversal and device acceptance require separate checks."
            }
        };
        SessionState.SetString(Key + "report", JsonUtility.ToJson(report));
        SessionState.SetBool(Key + "active", true);
        SessionState.SetBool(Key + "ownsPlay", startPlay);
        SessionState.SetInt(Key + "stage", startPlay ? -1 : 0);
        SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 90f);
        SessionState.SetFloat(Key + "due", (float)EditorApplication.timeSinceStartup + 1f);
        SessionState.SetInt(Key + "testPhoto", 0);
        SessionState.SetBool(Key + "captureSkippedForCapacity", false);
        SessionState.SetInt(Key + "musicSource", 0);
        SessionState.SetInt(Key + "shutterSource", 0);
        SessionState.SetBool(Key + "snapshotTaken", false);
        SaveReport(report);
        if (startPlay) EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetInt(Key + "stage", 0);
            SessionState.SetFloat(Key + "due", (float)EditorApplication.timeSinceStartup + 1f);
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            Check(false, "Validation completed before Play Mode exit", "Play Mode was stopped before validation finished.");
            Finish(false, false);
        }
    }

    private static void BeforeAssemblyReload()
    {
        if (!SessionState.GetBool(Key + "active", false) || activityOverride == null) return;
        try { Check(false, "Validation completed before assembly reload", "Compilation interrupted the runtime checks."); }
        finally { Finish(false, false); }
    }

    private static RayBanDisplayShell FindShell() => UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(s => s.gameObject.scene.IsValid() && s.gameObject.scene.isLoaded && s.router != null);

    private static void Update()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0))
        { Check(false, "Validation deadline", "Runtime validation exceeded 90 seconds."); Finish(false); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "due", 0)) return;
        try
        {
            var shell = FindShell();
            Require(shell != null, "Live shell exists");
            if (SessionState.GetInt(Key + "stage", -1) > 0 && activityOverride == null)
                throw new InvalidOperationException("The isolated activity store was lost; stopping before another test action.");
            switch (SessionState.GetInt(Key + "stage", -1))
            {
                case 0:
                    BeginActivityIsolation();
                    Snapshot(shell);
                    Check(shell.UsesLiveFeatures, "Live feature shell owns display");
                    Require(shell.cameraBridge != null && shell.mapBridge != null && shell.musicBridge != null &&
                        shell.assistantBridge != null && shell.captionBridge != null, "All five backend bridges assigned");
                    shell.ShowLauncher();
                    Check(shell.IsVisible, "Launcher opens");
                    int apps = shell.VisualRoot.GetComponentsInChildren<RayBanTile>(true).Count(t => t.PageId == 2);
                    Check(apps == 12, "Twelve launcher apps", "count=" + apps);
                    int pages = Enumerable.Range(0, 3).Count(p => shell.VisualRoot.Find("Dashboard " + p) != null);
                    Check(pages == 3, "Three dashboard pages", "count=" + pages);
                    for (int p = 0; p < 3; p++) { shell.SetPage(p); Check(shell.CurrentPage == p, "Dashboard " + p + " selection"); }
                    var tabs = shell.VisualRoot.GetComponentsInChildren<RayBanPageTab>(true);
                    Check(tabs.Length == 3 && tabs.Select(t => t.page).Distinct().Count() == 3, "Three clickable dashboard tabs");
                    foreach (var tab in tabs)
                    {
                        tab.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                        Check(shell.CurrentPage == tab.page, "Dashboard tab " + tab.page + " pointer click");
                    }
                    CheckConnectedPageRouting(shell);
                    CheckSettings(shell);
                    shell.router.CloseEntireUI();
                    Check(!shell.IsVisible && !shell.router.IsUIVisible, "Menu router closes upgraded display");
                    shell.router.OpenEntireUI();
                    Check(shell.IsVisible && shell.router.IsUIVisible, "Menu router reopens upgraded display");
                    shell.OpenApp("Camera");
                    Next(1, 1f);
                    break;
                case 1:
                    Require(shell.cameraBridge.capture != null, "Existing gameplay camera backend assigned");
                    RenderCameraIfNeeded(shell, false);
                    Check(shell.cameraBridge.capture.isActiveAndEnabled, "Nested camera backend activated");
                    Check(shell.cameraBridge.PreviewTexture != null, "Gameplay preview has rendered frame");
                    Check(!shell.cameraBridge.capture.GetComponentsInChildren<Canvas>(true).Any(c => c.isActiveAndEnabled), "Legacy camera Canvas hidden");
                    CheckSyntheticTwist(shell, true);
                    CaptureScreen(shell, "camera");
                    SessionState.SetInt(Key + "photoCount", shell.cameraBridge.CapturedCount);
                    SessionState.SetInt(Key + "activityCountBeforeCapture", testActivityStore.Items.Count);
                    SessionState.SetInt(Key + "photoActivityBeforeCapture", testActivityStore.Items.Count(item => item.Kind == QuadNotificationKind.PhotoSaved));
                    if (shell.cameraBridge.CapturedCount >= shell.cameraBridge.PhotoCapacity)
                    {
                        SessionState.SetBool(Key + "captureSkippedForCapacity", true);
                        Check(!shell.cameraBridge.CanCapture, "Full album prevents another capture");
                        Note("Capture, crop-dimension and new-photo display checks were skipped because the local album is full. Existing photos were preserved; free a slot manually before rerunning capture coverage.");
                    }
                    else if (shell.cameraBridge.CanCapture)
                    {
                        shell.cameraBridge.Capture(new Rect(.25f, .25f, .5f, .5f));
                        RenderCameraIfNeeded(shell, true);
                    }
                    else Check(false, "Capture can start", shell.cameraBridge.Status);
                    SessionState.SetFloat(Key + "captureUntil", (float)EditorApplication.timeSinceStartup + 6f);
                    Next(2, .3f);
                    break;
                case 2:
                    if (shell.cameraBridge.IsCapturing && EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "captureUntil", 0))
                    { RenderCameraIfNeeded(shell, true); Next(2, .15f); break; }
                    int originalCount = SessionState.GetInt(Key + "photoCount", 0);
                    bool captureSkipped = SessionState.GetBool(Key + "captureSkippedForCapacity", false);
                    bool captured = shell.cameraBridge.CapturedCount == originalCount + 1;
                    if (captureSkipped)
                        Check(shell.cameraBridge.CapturedCount == originalCount, "Full album preserved during validation");
                    else
                        Check(captured, "Gameplay capture stored in shared gallery", "before=" + originalCount + ", after=" + shell.cameraBridge.CapturedCount);
                    if (captured && !captureSkipped)
                    {
                        Check(testActivityStore.Items.Count == SessionState.GetInt(Key + "activityCountBeforeCapture", 0) + 1 &&
                              testActivityStore.Items.Count(item => item.Kind == QuadNotificationKind.PhotoSaved) == SessionState.GetInt(Key + "photoActivityBeforeCapture", 0) + 1,
                            "Successful capture publishes exactly one PhotoSaved activity entry");
                        Check(string.IsNullOrEmpty(testActivityStore.LastStorageError), "Capture activity saves in the isolated temporary store");
                        var photo = shell.cameraBridge.Photos[originalCount];
                        SessionState.SetInt(Key + "testPhoto", photo != null ? photo.GetInstanceID() : 0);
                        int cropWidth = Mathf.Max(1, Mathf.RoundToInt(shell.cameraBridge.capture.targetRT.width * .5f));
                        int cropHeight = Mathf.Max(1, Mathf.RoundToInt(shell.cameraBridge.capture.targetRT.height * .5f));
                        float boundedScale = Mathf.Min(1f, (float)GalleryDataManager.MaxPhotoEdge / Mathf.Max(cropWidth, cropHeight));
                        int expectedWidth = Mathf.Max(1, Mathf.RoundToInt(cropWidth * boundedScale));
                        int expectedHeight = Mathf.Max(1, Mathf.RoundToInt(cropHeight * boundedScale));
                        Check(photo != null && photo.width == expectedWidth && photo.height == expectedHeight,
                            "Captured dimensions preserve viewfinder crop within album size limit",
                            "expected=" + expectedWidth + "x" + expectedHeight);
                    }
                    else
                        Check(testActivityStore.Items.Count == SessionState.GetInt(Key + "activityCountBeforeCapture", 0) &&
                              testActivityStore.Items.Count(item => item.Kind == QuadNotificationKind.PhotoSaved) == SessionState.GetInt(Key + "photoActivityBeforeCapture", 0),
                            captureSkipped ? "Capacity-skipped capture publishes no activity" : "Unsuccessful capture publishes no PhotoSaved activity");
                    shell.OpenApp("Photos");
                    var testPhoto = FindInstance<Texture2D>(SessionState.GetInt(Key + "testPhoto", 0));
                    if (!captureSkipped)
                        Check(testPhoto != null && shell.FeatureScreens.GetComponentsInChildren<RawImage>(true).Any(i => i.isActiveAndEnabled && i.texture == testPhoto), "Photos displays captured gameplay image");
                    Check(shell.cameraBridge.PreviewTexture == null, "Camera lease released outside Camera/Assistant");
                    shell.Back();
                    Check(string.IsNullOrEmpty(shell.CurrentDetail), "Photos Back returns to launcher");
                    shell.OpenApp("Maps");
                    Next(3, .3f);
                    break;
                case 3:
                    var map = shell.mapBridge;
                    Check(map.IsAcquired && map.navigator != null && map.navigator.isActiveAndEnabled, "Nested Maps backend activated");
                    var registry = Resources.Load<MapConfig>("Map_Settings");
                    Check(registry != null && registry.landmarks != null && registry.landmarks.Count > 0 &&
                        map.LandmarkCount == registry.landmarks.Count, "All registered local landmarks available",
                        "count=" + map.LandmarkCount);
                    Check(map.HasPlayer, "Map player camera resolved");
                    Check(map.navigator != null && !map.navigator.GetComponentsInChildren<Canvas>(true).Any(c => c.isActiveAndEnabled), "Legacy Maps Canvas hidden");
                    int selected = map.SelectedIndex;
                    map.Execute("maps.next");
                    Check(map.LandmarkCount > 1 && map.SelectedIndex != selected, "Next landmark control");
                    map.Execute("maps.previous");
                    Check(map.SelectedIndex == selected, "Previous landmark control");
                    map.Execute("maps.start");
                    Check(map.IsNavigating && !string.IsNullOrWhiteSpace(map.DestinationName), "Local landmark navigation starts");
                    Check(map.HasPlayer && !float.IsNaN(map.Distance) && !float.IsInfinity(map.Distance) && map.Distance >= 0f &&
                        !float.IsNaN(map.HeadingAngle), "Finite local direction and distance");
                    // Allow one runtime Update to refresh the visible guidance before capturing.
                    Next(11, .2f);
                    break;
                case 11:
                    CaptureScreen(shell, "maps");
                    map = shell.mapBridge;
                    map.Execute("maps.end");
                    Check(!map.IsNavigating, "End local navigation");
                    shell.Back();
                    Check(!map.IsAcquired && (map.navigator == null || !map.navigator.gameObject.activeInHierarchy), "Maps Back releases nested backend");
                    shell.OpenApp("Music");
                    Next(4, .3f);
                    break;
                case 4:
                    var music = shell.musicBridge;
                    Check(music.IsAcquired && music.player != null && music.player.isActiveAndEnabled, "Nested Music backend activated");
                    Check(music.IsAvailable && music.CurrentTrack != null, "Existing music playlist available");
                    Check(!music.IsPlaying, "Opening Music does not autoplay");
                    CheckSyntheticTwist(shell, false);
                    music.Toggle();
                    Next(5, .4f);
                    break;
                case 5:
                    Check(shell.musicBridge.IsPlaying, "Muted music playback starts");
                    CaptureScreen(shell, "music");
                    if (shell.musicBridge.player != null) shell.musicBridge.player.Pause();
                    Next(6, .15f);
                    break;
                case 6:
                    Check(!shell.musicBridge.IsPlaying, "Music pauses");
                    shell.musicBridge.Release();
                    Check(!shell.musicBridge.IsAcquired && (shell.musicBridge.player == null || !shell.musicBridge.player.IsPlaying), "Music release stops playback");
                    shell.Back();
                    shell.OpenApp("Assistant");
                    Next(7, 1f);
                    break;
                case 7:
                    var assistant = shell.assistantBridge;
                    Check(assistant.IsAcquired && assistant.manager != null && assistant.manager.isActiveAndEnabled, "Assistant backend acquired");
                    Check(assistant.manager != null && assistant.manager.IsInitialized && assistant.manager.wakeWordManager != null && assistant.manager.wakeWordManager.IsInitialized, "Assistant initializes");
                    Check(!assistant.IsBusy && !assistant.IsListening, "Assistant opens idle without Listen request");
                    CaptureScreen(shell, "assistant-idle");
                    shell.Back();
                    Next(8, .4f);
                    break;
                case 8:
                    Check(!shell.assistantBridge.IsAcquired && !shell.assistantBridge.IsBusy && !shell.assistantBridge.IsListening, "Assistant close releases recognition lease");
                    Check(shell.assistantBridge.manager != null && !shell.assistantBridge.manager.IsResponding &&
                        (shell.assistantBridge.manager.wakeWordManager == null || !shell.assistantBridge.manager.wakeWordManager.IsVoiceActivated), "Assistant response and wake state canceled on close");
                    Check(shell.assistantBridge.serviceRoot != null && !shell.assistantBridge.serviceRoot.activeInHierarchy, "Assistant service group closed");
                    shell.OpenApp("Captions");
                    Next(9, .3f);
                    break;
                case 9:
                    Check(shell.captionBridge.IsAcquired && shell.captionBridge.manager != null && shell.captionBridge.manager.isActiveAndEnabled, "Nested Captions backend activated");
                    Check(!shell.captionBridge.IsListening && !shell.captionBridge.IsBusy, "Captions opens idle without translation request");
                    shell.Back();
                    Next(10, .2f);
                    break;
                case 10:
                    Check(!shell.captionBridge.IsAcquired && !shell.captionBridge.IsListening &&
                        (shell.captionBridge.manager == null || !shell.captionBridge.manager.gameObject.activeInHierarchy), "Captions close releases nested backend");
                    Check(string.IsNullOrEmpty(shell.CurrentDetail) && shell.IsVisible, "Feature Back returns to launcher");
                    int cameraDelta = Count<Camera>() - SessionState.GetInt(Key + "cameras", -1);
                    if (cameraDelta == 0) Check(true, "Camera count unchanged after feature lifecycle");
                    else Note("Camera count changed by " + cameraDelta + " during feature initialization; inspect SDK-owned camera creation separately.");
                    Check(Count<OVRCameraRig>() == SessionState.GetInt(Key + "rigs", -1), "No additional player rigs created");
                    Check(Count<EventSystem>() == SessionState.GetInt(Key + "events", -1), "No additional EventSystems created");
                    Finish(true);
                    break;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }
        catch (Exception exception)
        {
            Check(false, "Runtime check exception", exception.GetType().Name + ": " + exception.Message);
            Finish(false);
        }
    }

    private static int Count<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    private static T FindInstance<T>(int id) where T : UnityEngine.Object =>
        id == 0 ? null : Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(item => item.GetInstanceID() == id);
    private static void Note(string message)
    {
        var report = ReadReport();
        if (!report.limitations.Contains(message)) report.limitations.Add(message);
        SaveReport(report);
    }
    private static void CaptureScreen(RayBanDisplayShell shell, string feature)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Diagnostics/2026-11-01-QuestValidation/inapp-runtime-" + feature + ".png"));
        try
        {
            QuadRayBanDisplayPreview.Capture(shell, path);
            Check(File.Exists(path) && new FileInfo(path).Length > 0, feature + " runtime UI capture", path);
        }
        catch (Exception exception) { Check(false, feature + " runtime UI capture", exception.GetType().Name + ": " + exception.Message); }
    }
    private static void CheckSettings(RayBanDisplayShell shell)
    {
        Require(shell.appSettings != null && shell.appSettings.IsActive, "App settings active");
        try
        {
            shell.appSettings.SetVolume(.4f);
            Check(Mathf.Approximately(shell.appSettings.Volume, .4f) && Mathf.Approximately(AudioListener.volume, .4f) &&
                Mathf.Approximately(PlayerPrefs.GetFloat(RayBanAppSettings.VolumeKey, -1f), .4f), "App volume applies and saves");
            shell.appSettings.SetBrightness(.6f);
            var graphic = shell.VisualRoot.GetComponentsInChildren<Graphic>(true).FirstOrDefault(g => g.isActiveAndEnabled);
            Check(Mathf.Approximately(shell.appSettings.Brightness, .6f) &&
                Mathf.Approximately(PlayerPrefs.GetFloat(RayBanAppSettings.BrightnessKey, -1f), .6f) && graphic != null &&
                Mathf.Approximately(graphic.canvasRenderer.GetColor().r, .6f), "Display brightness applies and saves");
        }
        finally { RestoreSettings(shell); }
        Check(PlayerPrefs.HasKey(RayBanAppSettings.VolumeKey) == SessionState.GetBool(Key + "volumeHadKey", false) &&
            PlayerPrefs.HasKey(RayBanAppSettings.BrightnessKey) == SessionState.GetBool(Key + "brightnessHadKey", false), "Settings preference presence restored");
        Note("Audio only hides the app display; Do not disturb controls this app's activity indicator. These checks do not assert headset system audio/notification integration.");
    }
    private static void CheckConnectedPageRouting(RayBanDisplayShell shell)
    {
        // These are labelled local samples, with no social account or delivery path.
        foreach (var target in RayBanExternalAppCatalog.Targets)
        {
            shell.OpenApp(target.AppName);
            string expectedView = target.AppName == "Instagram" ? "instagram" : target.AppName == "Calls" ? "calls" : "messages";
            Check(shell.FeatureScreens.CurrentView == expectedView, target.AppName + " opens its in-app sample screen");
            Check(!shell.FeatureScreens.Controls.Any(t => t != null && t.gameObject.activeInHierarchy &&
                  t.ActionId != null && t.ActionId.StartsWith(RayBanExternalAppCatalog.OpenActionPrefix, StringComparison.Ordinal)),
                target.AppName + " exposes no active browser handoff control");
            var texts = shell.FeatureScreens.GetComponentsInChildren<TMPro.TMP_Text>().Where(t => t.isActiveAndEnabled).ToArray();
            Check(texts.Any(t => t.name == "Demo badge" && t.text == "SAMPLE") &&
                  texts.Any(t => t.text.Contains("Account not connected")),
                target.AppName + " labels sample content and disconnected account state");
            shell.Back();
            Check(string.IsNullOrEmpty(shell.CurrentDetail), target.AppName + " Back returns to launcher");
        }
        shell.SetPage(1);
        shell.OpenApp("Notifications");
        Check(shell.FeatureScreens.CurrentView == "notifications" &&
              shell.FeatureScreens.Controls.Any(t => t != null && t.gameObject.activeInHierarchy && t.ActionId == "activity.settings") &&
              !shell.FeatureScreens.Controls.Any(t => t != null && t.gameObject.activeInHierarchy && t.ActionId == "notification.done"),
            "Notifications opens local app activity inbox instead of sample messages");
        shell.Back();
        Check(string.IsNullOrEmpty(shell.CurrentDetail) && shell.CurrentPage == 1, "App activity Back returns to notification dashboard");
        Note("Social and Calls checks exercise labelled in-app sample screens only; no provider account is connected and no messages or calls are delivered.");
    }
    private static void RestoreSettings(RayBanDisplayShell shell)
    {
        if (!SessionState.GetBool(Key + "snapshotTaken", false)) return;
        if (shell != null && shell.appSettings != null && shell.appSettings.IsActive)
        {
            shell.appSettings.SetVolume(SessionState.GetFloat(Key + "appVolume", 1f));
            shell.appSettings.SetBrightness(SessionState.GetFloat(Key + "appBrightness", 1f));
        }
        AudioListener.volume = SessionState.GetFloat(Key + "listenerVolume", 1f);
        RestorePreference(RayBanAppSettings.VolumeKey, "volume");
        RestorePreference(RayBanAppSettings.BrightnessKey, "brightness");
        PlayerPrefs.Save();
    }
    private static void RestorePreference(string preference, string suffix)
    {
        if (SessionState.GetBool(Key + suffix + "HadKey", false)) PlayerPrefs.SetFloat(preference, SessionState.GetFloat(Key + suffix + "Preference", 1f));
        else PlayerPrefs.DeleteKey(preference);
    }
    private static void CheckSyntheticTwist(RayBanDisplayShell shell, bool camera)
    {
        var features = shell.FeatureScreens;
        var serialized = new SerializedObject(features);
        var property = serialized.FindProperty("mediaTwistSource");
        var source = property != null ? property.objectReferenceValue as PinchAndTwistEventSource : null;
        if (source == null || !source.isActiveAndEnabled)
        {
            Check(false, (camera ? "Camera" : "Music") + " twist source available");
            return;
        }
        float before = camera ? serialized.FindProperty("mediaZoom").floatValue : shell.musicBridge.Volume;
        float delta = before > (camera ? 2f : .5f) ? -.2f : .2f;
        try
        {
            source.OnStartPinchAndTwist.Invoke();
            source.OnPinchAndTwist.Invoke(delta);
            source.OnEndPinchAndTwist.Invoke();
            serialized.Update();
            float after = camera ? serialized.FindProperty("mediaZoom").floatValue : shell.musicBridge.Volume;
            Check(Mathf.Approximately(after, before + delta * (camera ? 2f : 1f)),
                camera ? "Synthetic twist adjusts camera zoom" : "Synthetic twist adjusts music volume",
                "before=" + before + ", after=" + after);
        }
        finally
        {
            source.OnStartPinchAndTwist.Invoke();
            source.OnPinchAndTwist.Invoke(-delta);
            source.OnEndPinchAndTwist.Invoke();
            if (!camera) shell.musicBridge.SetVolume(before);
        }
        Note("Synthetic pinch-and-twist UnityEvents exercise existing zoom/volume subscribers; actual optical hand recognition is not tested.");
    }
    private static void Next(int stage, float delay)
    {
        SessionState.SetInt(Key + "stage", stage);
        SessionState.SetFloat(Key + "due", (float)EditorApplication.timeSinceStartup + delay);
    }
    private static Report ReadReport() => JsonUtility.FromJson<Report>(SessionState.GetString(Key + "report", "{}"));
    private static void Check(bool success, string name, string detail = "")
    {
        var report = ReadReport();
        report.checks.Add(new CheckResult { name = name, passed = success, detail = detail });
        if (success) report.passed++; else report.failed++;
        SaveReport(report);
    }
    private static void Require(bool success, string name)
    {
        Check(success, name);
        if (!success) throw new InvalidOperationException(name);
    }
    private static void SaveReport(Report report)
    {
        string json = JsonUtility.ToJson(report, true);
        SessionState.SetString(Key + "report", json);
        string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, json);
    }
    private static void Snapshot(RayBanDisplayShell shell)
    {
        SessionState.SetBool(Key + "snapshotTaken", true);
        SessionState.SetInt(Key + "cameras", Count<Camera>());
        SessionState.SetInt(Key + "rigs", Count<OVRCameraRig>());
        SessionState.SetInt(Key + "events", Count<EventSystem>());
        SessionState.SetBool(Key + "wasVisible", shell.IsVisible);
        SessionState.SetString(Key + "detail", shell.CurrentDetail ?? "");
        SessionState.SetInt(Key + "page", shell.CurrentPage);
        SessionState.SetBool(Key + "volumeHadKey", PlayerPrefs.HasKey(RayBanAppSettings.VolumeKey));
        SessionState.SetBool(Key + "brightnessHadKey", PlayerPrefs.HasKey(RayBanAppSettings.BrightnessKey));
        SessionState.SetFloat(Key + "volumePreference", PlayerPrefs.GetFloat(RayBanAppSettings.VolumeKey, 1f));
        SessionState.SetFloat(Key + "brightnessPreference", PlayerPrefs.GetFloat(RayBanAppSettings.BrightnessKey, 1f));
        SessionState.SetFloat(Key + "appVolume", shell.appSettings != null ? shell.appSettings.Volume : 1f);
        SessionState.SetFloat(Key + "appBrightness", shell.appSettings != null ? shell.appSettings.Brightness : 1f);
        SessionState.SetFloat(Key + "listenerVolume", AudioListener.volume);
        var map = shell.mapBridge != null ? shell.mapBridge.navigator : null;
        SessionState.SetInt(Key + "mapSelection", map != null && map.IsNavigating ? map.SelectedLandmark : -1);
        var music = shell.musicBridge != null ? shell.musicBridge.player : null;
        SessionState.SetBool(Key + "musicPlaying", music != null && music.IsPlaying);
        SessionState.SetBool(Key + "musicAcquired", shell.musicBridge != null && shell.musicBridge.IsAcquired);
        RememberMute("musicSource", music != null ? music.audioSource : null);
        RememberMute("shutterSource", shell.cameraBridge != null && shell.cameraBridge.capture != null ? shell.cameraBridge.capture.audioSource : null);
    }
    private static void RememberMute(string key, AudioSource source)
    {
        if (source == null) return;
        SessionState.SetInt(Key + key, source.GetInstanceID());
        SessionState.SetBool(Key + key + "Mute", source.mute);
        source.mute = true;
    }
    private static void RestoreMute(string key)
    {
        var source = FindInstance<AudioSource>(SessionState.GetInt(Key + key, 0));
        if (source != null) source.mute = SessionState.GetBool(Key + key + "Mute", false);
    }
    private static void RenderCameraIfNeeded(RayBanDisplayShell shell, bool force)
    {
        var bridge = shell.cameraBridge;
        if (bridge == null || bridge.capture == null || bridge.capture.snapshotCamera == null || (!force && bridge.PreviewTexture != null)) return;
        var camera = bridge.capture.snapshotCamera;
        if (!camera.gameObject.activeInHierarchy || camera.targetTexture == null) return;
        camera.Render();
        var report = ReadReport();
        report.manuallyRenderedCamera = true;
        SaveReport(report);
    }
    private static void BeginActivityIsolation()
    {
        if (!Application.isPlaying || activityOverride != null)
            throw new InvalidOperationException("Create activity isolation once, after entering Play Mode.");
        string candidate = Path.GetFullPath(Path.Combine(Path.GetTempPath(), ActivityTempPrefix + Guid.NewGuid().ToString("N")));
        if (Directory.Exists(candidate)) throw new IOException("The unique activity-check directory already exists.");
        Directory.CreateDirectory(candidate);
        activityTempDirectory = candidate;
        originalActivityStore = RayBanLocalNotifications.Store;
        testActivityStore = new QuadLocalNotificationStore(candidate);
        activityOverride = RayBanLocalNotifications.UseStoreForEditorChecks(testActivityStore);
        Check(ReferenceEquals(RayBanLocalNotifications.Store, testActivityStore) && testActivityStore.Items.Count == 0,
            "Runtime activity writes are isolated from the user's history");
    }
    private static void RestoreActivityIsolation()
    {
        var previous = originalActivityStore;
        try
        {
            activityOverride?.Dispose();
            if (previous != null)
                Check(ReferenceEquals(RayBanLocalNotifications.Store, previous), "Original notification store restored");
        }
        finally
        {
            activityOverride = null;
            originalActivityStore = testActivityStore = null;
            string directory = activityTempDirectory;
            activityTempDirectory = null;
            if (!string.IsNullOrEmpty(directory)) RemoveActivityTempDirectory(directory);
        }
    }
    private static void RemoveActivityTempDirectory(string directory)
    {
        string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(full);
        if (!string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase) ||
            !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(ActivityTempPrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name.Substring(ActivityTempPrefix.Length), "N", out _))
            throw new InvalidOperationException("Refusing to remove a path outside the unique activity-check directory.");
        if (!Directory.Exists(full)) return;
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to remove a redirected activity-check directory.");
        // This store creates only these files. Avoid recursive deletion of any
        // unexpected content even within the verified temporary directory.
        File.Delete(Path.Combine(full, QuadLocalNotificationStore.StorageFileName));
        File.Delete(Path.Combine(full, QuadLocalNotificationStore.StorageFileName + ".tmp"));
        Directory.Delete(full, false);
    }
    private static void Finish(bool completed, bool stopOwnedPlay = true)
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        bool ownsPlay = SessionState.GetBool(Key + "ownsPlay", false);
        SessionState.SetBool(Key + "active", false);
        try
        {
            var shell = FindShell();
            if (shell != null && Application.isPlaying && SessionState.GetBool(Key + "snapshotTaken", false))
            {
                shell.Hide();
                if (shell.cameraBridge != null) shell.cameraBridge.SetCameraVisible(false);
                if (shell.mapBridge != null) shell.mapBridge.Release();
                if (shell.musicBridge != null) shell.musicBridge.Release();
                if (shell.assistantBridge != null) shell.assistantBridge.Release();
                if (shell.captionBridge != null) shell.captionBridge.Release();
                var nav = shell.mapBridge != null ? shell.mapBridge.navigator : null;
                int previousLandmark = SessionState.GetInt(Key + "mapSelection", -1);
                if (nav != null) { if (previousLandmark >= 0) nav.SelectDestination(previousLandmark); else nav.StopNavigation(); }
                var photo = FindInstance<Texture2D>(SessionState.GetInt(Key + "testPhoto", 0));
                if (photo != null)
                {
                    if (shell.cameraBridge != null && shell.cameraBridge.capture != null && shell.cameraBridge.capture.recentPhotoPreview != null &&
                        shell.cameraBridge.capture.recentPhotoPreview.texture == photo) shell.cameraBridge.capture.recentPhotoPreview.texture = null;
                    int photoIndex = GalleryDataManager.SavedPhotos.IndexOf(photo);
                    if (photoIndex >= 0 && !GalleryDataManager.TryDelete(photoIndex, out string cleanupError))
                        Debug.LogWarning("[Quad runtime checks] Could not remove test photo: " + cleanupError);
                }
                RestoreMute("musicSource");
                RestoreMute("shutterSource");
                RestoreSettings(shell);
                if (!ownsPlay)
                {
                    shell.SetPage(SessionState.GetInt(Key + "page", 2));
                    if (SessionState.GetBool(Key + "wasVisible", false))
                    {
                        string detail = SessionState.GetString(Key + "detail", "");
                        if (string.IsNullOrEmpty(detail)) shell.ShowLauncher(); else shell.OpenApp(detail);
                    }
                    if (SessionState.GetBool(Key + "musicAcquired", false) && shell.musicBridge != null)
                    {
                        shell.musicBridge.Acquire();
                        if (SessionState.GetBool(Key + "musicPlaying", false) && shell.musicBridge.player != null) shell.musicBridge.player.Play();
                    }
                }
            }
        }
        catch (Exception exception) { Check(false, "Cleanup", exception.GetType().Name + ": " + exception.Message); }
        finally
        {
            try
            {
                RestoreActivityIsolation();
                var shell = FindShell();
                if (shell != null && Application.isPlaying)
                {
                    shell.RefreshLocalNotifications(true);
                    if (shell.FeatureScreens != null) shell.FeatureScreens.RefreshLocalNotificationView(true);
                }
            }
            catch (Exception exception) { Check(false, "Activity isolation cleanup", exception.GetType().Name + ": " + exception.Message); }
        }
        var report = ReadReport();
        report.finishedUtc = DateTime.UtcNow.ToString("O");
        report.status = completed && report.failed == 0 ? "passed" : "failed";
        SaveReport(report);
        Debug.Log("[QuadRayBanRuntimeChecks] " + report.status + ": " + report.passed + " passed, " + report.failed + " failed. " + OutputPath);
        if (ownsPlay && stopOwnedPlay && EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}
#endif
