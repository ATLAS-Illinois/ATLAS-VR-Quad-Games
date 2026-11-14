#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Nonblocking Editor regression for the real ISDK-to-uGUI dispatch boundary.
/// Injects only PointableCanvas pointer events; Unity's normal EventSystem.Update
/// must deliver the click and drag. No direct button/module Process calls.
/// </summary>
[InitializeOnLoad]
public static class QuadPointerDispatchChecks
{
    private const string Key = "QuadPointerDispatchChecks.";
    private const string OutputPath = "Diagnostics/2026-11-01-QuestValidation/inapp-pointer-dispatch-checks.json";
    private const int PointerId = 2147001201;
    [Serializable] private sealed class Entry { public string name, detail; public bool passed; }
    [Serializable] private sealed class Report
    {
        public string startedUtc, finishedUtc, scene, status;
        public bool startedPlayMode;
        public int passed, failed;
        public List<Entry> checks = new List<Entry>();
        public string limitation = "Synthetic SDK pointer input in Editor. Exercises actual EventSystem frame dispatch, uGUI clicks and ScrollRect drag; does not prove physical hand/controller tracking or Android behavior. Opens the in-app WhatsApp sample screen; no connected account, delivered message, browser, cloud or microphone calls.";
    }

    private static RayBanDisplayShell shell;
    private static PointableCanvas pointable;
    private static PointableCanvasModule module;
    private static EventSystem owner, standalone, previousCurrent;
    private static StandaloneInputModule standaloneModule;
    private static RayBanTile clickTile;
    private static ScrollRect scroll;
    private static GameObject previousSelection;
    private static bool snapshotted, pointerActive, previousVisible, standaloneEnabled, standaloneActive, standaloneModuleEnabled;
    private static int previousPage, buttonClicks, nextFrame;
    private static string previousDetail;
    private static Vector2 previousScroll, previousVelocity;
    private static Vector3 dragStartLocal, lastLocal;
    private static float initialDragOffset;
    private static float previousCanvasAlpha;
    private static bool previousCanvasInteractable, previousCanvasRaycasts;

    static QuadPointerDispatchChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Ray-Ban Display/Validate ISDK pointer dispatch")]
    public static void StartChecks()
    {
        if (SessionState.GetBool(Key + "active", false)) throw new InvalidOperationException("Pointer dispatch validation is already running.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for compilation/import before validation.");
        var candidate = FindShell();
        if (candidate == null || candidate.VisualRoot == null) throw new InvalidOperationException("Open the authored Quad scene with its Ray-Ban display.");
        // Avoid interrupting live service ownership when joining an existing session.
        if (EditorApplication.isPlaying && !string.IsNullOrEmpty(candidate.CurrentDetail) && candidate.CurrentDetail != "WhatsApp")
            throw new InvalidOperationException("Return to the launcher before running pointer checks; active features will not be interrupted.");
        if (EditorApplication.isPlaying && ((candidate.assistantBridge != null && (candidate.assistantBridge.IsBusy || candidate.assistantBridge.IsListening)) ||
            (candidate.captionBridge != null && (candidate.captionBridge.IsBusy || candidate.captionBridge.IsListening))))
            throw new InvalidOperationException("Close the voice/caption session before pointer validation.");
        bool ownsPlay = !EditorApplication.isPlaying;
        Save(new Report { startedUtc = DateTime.UtcNow.ToString("O"), scene = candidate.gameObject.scene.path, status = "running", startedPlayMode = ownsPlay });
        SessionState.SetBool(Key + "active", true);
        SessionState.SetBool(Key + "ownsPlay", ownsPlay);
        SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 90f);
        SessionState.SetInt(Key + "stage", ownsPlay ? -1 : 0);
        SessionState.SetFloat(Key + "due", (float)EditorApplication.timeSinceStartup + 1f);
        snapshotted = pointerActive = false;
        nextFrame = 0;
        if (ownsPlay) EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetInt(Key + "stage", 0);
            SessionState.SetFloat(Key + "due", (float)EditorApplication.timeSinceStartup + 1.5f);
            nextFrame = Time.frameCount + 2;
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            Check(false, "Completed before Play Mode exit", "Play Mode exited before pointer checks finished.");
            Finish(false, false);
        }
    }

    private static RayBanDisplayShell FindShell() => UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(s => s.gameObject.scene.IsValid() && s.gameObject.scene.isLoaded && s.router != null);

    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0))
        { Check(false, "Deadline", "Pointer checks exceeded 90 seconds."); Finish(false); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "due", 0) || Time.frameCount < nextFrame) return;
        try
        {
            switch (SessionState.GetInt(Key + "stage", -1))
            {
                case 0:
                    Prepare();
                    standalone.gameObject.SetActive(true);
                    standalone.enabled = true;
                    standaloneModule.enabled = true;
                    EventSystem.current = standalone;
                    Next(1);
                    break;
                case 1:
                    Require(EventSystem.current == standalone, "Duplicate standalone EventSystem owns baseline", OwnerDescription());
                    Require(owner.isActiveAndEnabled && module.isActiveAndEnabled, "ISDK module remains enabled during blocked baseline");
                    Send(PointerEventType.Hover, TileLocal(clickTile));
                    Next(2);
                    break;
                case 2:
                    Send(PointerEventType.Select, TileLocal(clickTile));
                    Next(3);
                    break;
                case 3:
                    Send(PointerEventType.Unselect, TileLocal(clickTile));
                    Next(4);
                    break;
                case 4:
                    Check(buttonClicks == 0 && string.IsNullOrEmpty(shell.CurrentDetail), "Competing current EventSystem blocks SDK click",
                        "buttonCallbacks=" + buttonClicks + "; detail=" + shell.CurrentDetail + "; " + OwnerDescription());
                    CancelPointer();
                    EventSystem.current = owner;
                    Next(5);
                    break;
                case 5:
                    Require(EventSystem.current == owner && owner.currentInputModule == module, "ISDK owner processes automatic Unity frames", OwnerDescription());
                    Send(PointerEventType.Hover, TileLocal(clickTile));
                    Next(6);
                    break;
                case 6:
                    Send(PointerEventType.Select, TileLocal(clickTile));
                    Next(7);
                    break;
                case 7:
                    Send(PointerEventType.Unselect, TileLocal(clickTile));
                    Next(8);
                    break;
                case 8:
                    Check(buttonClicks == 1 && shell.CurrentDetail == "WhatsApp" &&
                          shell.FeatureScreens.CurrentView == "messages",
                        "SDK release becomes one uGUI click and opens the in-app WhatsApp sample inbox",
                        "buttonCallbacks=" + buttonClicks + "; detail=" + shell.CurrentDetail + "; view=" + shell.FeatureScreens.CurrentView);
                    Check(!shell.FeatureScreens.Controls.Any(t => t != null && t.gameObject.activeInHierarchy &&
                          t.ActionId != null && t.ActionId.StartsWith(RayBanExternalAppCatalog.OpenActionPrefix, StringComparison.Ordinal)) &&
                          shell.FeatureScreens.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.isActiveAndEnabled &&
                              t.text.Contains("Account not connected")),
                        "WhatsApp stays in-app and visibly identifies its disconnected sample state");
                    CancelPointer();
                    shell.SetPage(2);
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1f;
                    Next(9);
                    break;
                case 9:
                    Require(string.IsNullOrEmpty(shell.CurrentDetail) && scroll.gameObject.activeInHierarchy, "Launcher ready for drag");
                    var dragTile = shell.VisualRoot.GetComponentsInChildren<RayBanTile>(true).First(t => t.PageId == 2 && t.Title == "Calls");
                    dragStartLocal = TileLocal(dragTile);
                    initialDragOffset = scroll.content.anchoredPosition.y;
                    Send(PointerEventType.Hover, dragStartLocal);
                    Next(10);
                    break;
                case 10:
                    Send(PointerEventType.Select, dragStartLocal);
                    Next(11);
                    break;
                case 11:
                    Send(PointerEventType.Move, dragStartLocal + Vector3.up * 45f);
                    Next(12);
                    break;
                case 12:
                    Send(PointerEventType.Move, dragStartLocal + Vector3.up * 100f);
                    Next(13);
                    break;
                case 13:
                    Send(PointerEventType.Move, dragStartLocal + Vector3.up * 160f);
                    Next(14);
                    break;
                case 14:
                    Send(PointerEventType.Unselect, dragStartLocal + Vector3.up * 160f);
                    Next(15);
                    break;
                case 15:
                    float delta = scroll.content.anchoredPosition.y - initialDragOffset;
                    Check(delta > 30f, "SDK pointer drag scrolls the actual launcher ScrollRect", "contentOffsetDelta=" + delta.ToString("F2") + "; normalized=" + scroll.verticalNormalizedPosition.ToString("F3"));
                    Check(string.IsNullOrEmpty(shell.CurrentDetail), "Dragging does not activate the Calls sample tile", "detail=" + shell.CurrentDetail);
                    CancelPointer();
                    Next(16);
                    break;
                case 16:
                    Finish(true);
                    break;
            }
        }
        catch (Exception exception)
        {
            Check(false, "Unhandled check failure", exception.GetType().Name + ": " + exception.Message);
            Finish(false);
        }
    }

    private static void Prepare()
    {
        shell = FindShell();
        Require(shell != null && shell.VisualRoot != null && shell.OwnsPreview, "Live authored shell exists");
        pointable = shell.VisualRoot.GetComponent<PointableCanvas>();
        Require(pointable != null && pointable.isActiveAndEnabled, "Authored PointableCanvas exists");
        module = UnityEngine.Object.FindObjectsByType<PointableCanvasModule>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(m => m.gameObject.scene == shell.gameObject.scene);
        owner = module.GetComponent<EventSystem>();
        Require(owner != null && owner.isActiveAndEnabled && module.isActiveAndEnabled, "Existing ISDK EventSystem and module are active");
        standalone = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(e => e != owner && e.gameObject.scene == shell.gameObject.scene && e.GetComponent<StandaloneInputModule>() != null);
        Require(standalone != null, "Existing standalone EventSystem available for controlled baseline");
        standaloneModule = standalone.GetComponent<StandaloneInputModule>();
        scroll = shell.VisualRoot.GetComponentsInChildren<ScrollRect>(true).Single(s => s.name == "App viewport");
        previousCurrent = EventSystem.current;
        previousSelection = previousCurrent != null ? previousCurrent.currentSelectedGameObject : null;
        standaloneActive = standalone.gameObject.activeSelf;
        standaloneEnabled = standalone.enabled;
        standaloneModuleEnabled = standaloneModule.enabled;
        previousVisible = shell.IsVisible;
        previousPage = shell.CurrentPage;
        previousDetail = shell.CurrentDetail;
        previousScroll = scroll.content.anchoredPosition;
        previousVelocity = scroll.velocity;
        if (shell.router.canvasGroup != null)
        {
            previousCanvasAlpha = shell.router.canvasGroup.alpha;
            previousCanvasInteractable = shell.router.canvasGroup.interactable;
            previousCanvasRaycasts = shell.router.canvasGroup.blocksRaycasts;
        }
        snapshotted = true;
        shell.router.ShowMainMenu();
        shell.SetPage(2);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        clickTile = shell.VisualRoot.GetComponentsInChildren<RayBanTile>(true).Single(t => t.PageId == 2 && t.Title == "WhatsApp");
        buttonClicks = 0;
        clickTile.onClick.AddListener(ObserveButtonClick);
    }

    private static Vector3 TileLocal(RayBanTile tile)
    {
        RectTransform rect = (RectTransform)tile.transform;
        return shell.VisualRoot.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
    }

    private static void Send(PointerEventType type, Vector3 local)
    {
        lastLocal = local;
        var pose = new Pose(shell.VisualRoot.TransformPoint(local), shell.VisualRoot.rotation);
        pointable.ProcessPointerEvent(new PointerEvent(PointerId, type, pose));
        if (type == PointerEventType.Hover) pointerActive = true;
        if (type == PointerEventType.Cancel || type == PointerEventType.Unhover) pointerActive = false;
    }

    private static void CancelPointer()
    {
        if (pointerActive && pointable != null && shell != null && shell.VisualRoot != null)
            Send(PointerEventType.Cancel, lastLocal);
        pointerActive = false;
    }

    private static void ObserveButtonClick() => buttonClicks++;

    private static void Next(int stage)
    {
        SessionState.SetInt(Key + "stage", stage);
        SessionState.SetFloat(Key + "due", (float)EditorApplication.timeSinceStartup + .15f);
        nextFrame = Time.frameCount + 2;
    }

    private static string OwnerDescription() => "current=" + (EventSystem.current != null ? EventSystem.current.name : "null") +
        "; currentModule=" + (EventSystem.current != null && EventSystem.current.currentInputModule != null ? EventSystem.current.currentInputModule.GetType().Name : "null");

    private static Report Read() => JsonUtility.FromJson<Report>(SessionState.GetString(Key + "report", "{}"));
    private static void Save(Report report)
    {
        string json = JsonUtility.ToJson(report, true);
        SessionState.SetString(Key + "report", json);
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, json);
    }
    private static void Check(bool passed, string name, string detail = "")
    {
        Report report = Read();
        report.checks.Add(new Entry { name = name, passed = passed, detail = detail });
        if (passed) report.passed++; else report.failed++;
        Save(report);
    }
    private static void Require(bool passed, string name, string detail = "")
    {
        Check(passed, name, detail);
        if (!passed) throw new InvalidOperationException(name);
    }

    private static void Finish(bool completed, bool stopOwnedPlay = true)
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        bool ownsPlay = SessionState.GetBool(Key + "ownsPlay", false);
        SessionState.SetBool(Key + "active", false);
        try
        {
            CancelPointer();
            if (clickTile != null) clickTile.onClick.RemoveListener(ObserveButtonClick);
            if (snapshotted)
            {
                if (standaloneModule != null) standaloneModule.enabled = standaloneModuleEnabled;
                if (standalone != null)
                {
                    standalone.enabled = standaloneEnabled;
                    standalone.gameObject.SetActive(standaloneActive);
                }
                if (previousCurrent != null && previousCurrent.isActiveAndEnabled) EventSystem.current = previousCurrent;
                if (shell != null && Application.isPlaying)
                {
                    shell.SetPage(previousPage);
                    if (!string.IsNullOrEmpty(previousDetail)) shell.OpenApp(previousDetail);
                    if (scroll != null)
                    {
                        scroll.StopMovement();
                        scroll.content.anchoredPosition = previousScroll;
                        scroll.velocity = previousVelocity;
                    }
                    if (!previousVisible) shell.Hide();
                    if (shell.router.canvasGroup != null)
                    {
                        shell.router.canvasGroup.alpha = previousCanvasAlpha;
                        shell.router.canvasGroup.interactable = previousCanvasInteractable;
                        shell.router.canvasGroup.blocksRaycasts = previousCanvasRaycasts;
                    }
                    if (previousCurrent != null)
                        previousCurrent.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                Check(standalone == null || (standalone.enabled == standaloneEnabled && standalone.gameObject.activeSelf == standaloneActive), "Original standalone EventSystem state restored");
            }
        }
        catch (Exception exception) { Check(false, "Cleanup", exception.GetType().Name + ": " + exception.Message); }
        snapshotted = false;
        Report report = Read();
        report.finishedUtc = DateTime.UtcNow.ToString("O");
        report.status = completed && report.failed == 0 ? "passed" : "failed";
        Save(report);
        Debug.Log("[QuadPointerDispatchChecks] " + report.status + ": " + report.passed + " passed, " + report.failed + " failed. " + OutputPath);
        if (ownsPlay && stopOwnedPlay && EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
}
#endif
