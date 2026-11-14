#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Offline activity-store and in-app social preview checks with isolated data and a browser-launch tripwire.</summary>
public static class QuadConnectedFeatureChecks
{
    private const string OutputDirectory = "Diagnostics/2026-11-01-QuestValidation";
    [Serializable] private sealed class Check { public string name; public bool passed; }
    [Serializable] private sealed class Report
    {
        public string finishedUtc, error, limitation;
        public int passed, failed;
        public List<Check> checks = new List<Check>();
    }
    [Serializable] private sealed class SavedEntry
    {
        public string id, title, message;
        public long createdUtcTicks;
        public bool isRead;
        public int kind;
    }
    [Serializable] private sealed class SavedPayload
    {
        public int version = 1;
        public bool doNotDisturb;
        public List<SavedEntry> entries = new List<SavedEntry>();
    }

    [MenuItem("Tools/Ray-Ban Display/Validate local activity and legacy target compatibility")]
    public static string RunModelChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run model checks in the idle Editor outside Play Mode.");
        var report = new Report { limitation = "Offline Editor model checks in a temporary folder. Legacy external target identifiers are checked only for serialized compatibility, not as supported launch actions. No browser, network, microphone, user activity store, photos, or preferences are accessed. Android storage requires separate device validation." };
        string root = CreateTempRoot();
        try
        {
            CheckCatalog(report);
            string directory = Path.Combine(root, "activity");
            var store = new QuadLocalNotificationStore(directory);
            Assert(report, store.Items.Count == 0 && store.UnreadCount == 0 && !store.DoNotDisturb, "New activity store is empty with DND off");
            Assert(report, !store.MarkRead("missing") && !store.Delete("missing") && !store.Clear(), "Unknown activity mutations are harmless");
            Assert(report, store.Publish((QuadNotificationKind)99, "Unsupported", "No handler") == null &&
                store.Publish(QuadNotificationKind.PhotoSaved, "  ", "No title") == null && store.Items.Count == 0,
                "Unsupported notification kinds and blank titles are rejected");
            string first = store.Publish(QuadNotificationKind.PhotoSaved, "Photo saved", "Saved in Photos.");
            string second = store.Publish(QuadNotificationKind.RouteArrived, "Destination reached", "Open Maps to choose another landmark.");
            Assert(report, first != null && second != null && first != second && store.Items.Count == 2 &&
                store.Items[0].Id == second && store.Items[1].Id == first, "New activity appears first with distinct identifiers");
            Assert(report, store.UnreadCount == 2 && store.MarkRead(second) && store.UnreadCount == 1 && !store.MarkRead(second),
                "Opening activity marks only that entry read and is idempotent");
            int revision = store.Revision;
            store.DoNotDisturb = true;
            Assert(report, store.DoNotDisturb && store.Items.Count == 2 && store.UnreadCount == 1 && store.Revision > revision,
                "DND changes presentation preference without discarding activity");
            string quiet = store.Publish(QuadNotificationKind.PhotoSaved, "Quiet capture", "Stored while DND is enabled.");
            Assert(report, quiet != null && store.Items[0].Id == quiet && store.UnreadCount == 2,
                "Activity is retained while DND is enabled");
            store = new QuadLocalNotificationStore(directory);
            Assert(report, store.DoNotDisturb && store.Items.Count == 3 && store.UnreadCount == 2 && store.Find(second).IsRead,
                "DND, read state and entries survive reload");
            Assert(report, store.Items[0].Id == quiet && store.Items[2].Id == first, "Newest-first ordering survives reload");
            Assert(report, store.Delete(first) && store.Find(first) == null && store.Items.Count == 2, "Delete removes only the selected entry");
            store = new QuadLocalNotificationStore(directory);
            Assert(report, store.Find(first) == null && store.Find(second) != null && store.Find(quiet) != null, "Deleted entry stays deleted after reload");
            Assert(report, store.Clear() && store.Items.Count == 0 && store.UnreadCount == 0 && store.DoNotDisturb,
                "Clear empties activity while preserving DND");
            store = new QuadLocalNotificationStore(directory);
            Assert(report, store.Items.Count == 0 && store.DoNotDisturb, "Clear persists across reload");

            var ids = new List<string>();
            for (int i = 0; i < QuadLocalNotificationStore.MaximumItems + 5; i++)
                ids.Add(store.Publish(QuadNotificationKind.PhotoSaved, "Capture " + i, "Local sample activity."));
            Assert(report, store.Items.Count == QuadLocalNotificationStore.MaximumItems && store.UnreadCount == QuadLocalNotificationStore.MaximumItems,
                "Inbox is bounded to twenty entries");
            Assert(report, store.Items[0].Id == ids.Last() && store.Find(ids[0]) == null && store.Find(ids[4]) == null && store.Find(ids[5]) != null,
                "Capacity evicts oldest entries and retains newest twenty");
            store = new QuadLocalNotificationStore(directory);
            Assert(report, store.Items.Count == QuadLocalNotificationStore.MaximumItems && store.Items[0].Id == ids.Last(),
                "Bounded inbox survives reload");
            Assert(report, new FileInfo(Path.Combine(directory, QuadLocalNotificationStore.StorageFileName)).Length <= QuadLocalNotificationStore.MaximumFileBytes &&
                !File.Exists(Path.Combine(directory, QuadLocalNotificationStore.StorageFileName + ".tmp")), "Save publishes a bounded complete file");

            string safeId = store.Publish(QuadNotificationKind.PhotoSaved, "<b>" + new string('x', 100) + "</b>", "\u202E\n" + new string('y', 300));
            var safe = store.Find(safeId);
            Assert(report, safe != null && safe.Title.Length <= 72 && safe.Message.Length <= 180 && !safe.Title.Contains("<") &&
                !safe.Title.Contains(">") && !safe.Message.Contains("\u202E") && !safe.Message.Contains("\n"),
                "Activity copy has bounded text without rich-text tags or format controls");

            CheckInvalidSavedStates(root, report);
            string blocked = Path.Combine(root, "directory-is-file");
            File.WriteAllText(blocked, "fixture");
            var unavailable = new QuadLocalNotificationStore(blocked);
            string sessionId = unavailable.Publish(QuadNotificationKind.PhotoSaved, "Unsaved activity", "Still available this session.");
            Assert(report, sessionId != null && unavailable.Find(sessionId) != null && !string.IsNullOrEmpty(unavailable.LastStorageError),
                "Write failure retains session activity and exposes storage error");
        }
        catch (Exception exception)
        {
            report.error = exception.GetType().Name + ": " + exception.Message;
            Assert(report, false, "Model harness completed without exception");
        }
        finally
        {
            RemoveTempRoot(root);
            Save(report, "inapp-model-checks.json");
        }
        return Summary("In-app model checks", report);
    }

    private static void CheckCatalog(Report report)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Instagram", "https://www.instagram.com/" }, { "WhatsApp", "https://web.whatsapp.com/" },
            { "Messenger", "https://www.messenger.com/" }, { "Messages", "https://messages.google.com/web/" },
            { "Calls", "https://meet.google.com/" }
        };
        Assert(report, RayBanExternalAppCatalog.Targets.Count == 5 && RayBanExternalAppCatalog.Targets.Select(t => t.ViewKey).Distinct().Count() == 5,
            "Five distinct legacy external identifiers remain available for compatibility");
        foreach (var target in RayBanExternalAppCatalog.Targets)
        {
            Assert(report, expected.TryGetValue(target.AppName, out string url) && target.Url == url &&
                Uri.TryCreate(target.Url, UriKind.Absolute, out Uri uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo),
                target.AppName + " retains its exact legacy HTTPS catalog value");
            Assert(report, RayBanExternalAppCatalog.TryGetByApp(target.AppName.ToLowerInvariant(), out var byApp) && byApp == target &&
                RayBanExternalAppCatalog.TryGetByKey(target.ViewKey, out var byKey) && byKey == target &&
                RayBanExternalAppCatalog.TryGetByAction(RayBanExternalAppCatalog.OpenActionPrefix + target.ViewKey, out var byAction) && byAction == target,
                target.AppName + " legacy identifiers resolve only through the fixed catalog");
        }
        string[] rejected = { null, "", "https://example.invalid/", "javascript:alert(1)", "file:///private", "instagram.evil", "external.instagram?next=https://example.invalid" };
        foreach (string candidate in rejected)
            Assert(report, !RayBanExternalAppCatalog.TryGetByApp(candidate, out _) && !RayBanExternalAppCatalog.TryGetByKey(candidate, out _) &&
                !RayBanExternalAppCatalog.TryGetByAction(RayBanExternalAppCatalog.OpenActionPrefix + candidate, out _),
                "Arbitrary external target rejected: " + (candidate ?? "null"));
    }

    /// <summary>Run after the parent has started an idle Play session. Restores the caller's display and injected services.</summary>
    public static string RunUIChecks()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run in-app UI checks in idle Play Mode.");
        var shell = UnityEngine.Object.FindObjectsByType<RayBanDisplayShell>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(s => s.gameObject.scene.IsValid() && s.gameObject.scene.isLoaded && s.router != null);
        if (shell == null || !shell.UsesLiveFeatures || shell.FeatureScreens == null || shell.assistantBridge == null || shell.captionBridge == null)
            throw new InvalidOperationException("The live Quad display and its voice bridges must be available.");
        if (!VoiceIdle(shell) || (shell.musicBridge != null && shell.musicBridge.IsAssistantAudioActive))
            throw new InvalidOperationException("Close the user's active voice session before validation.");

        var report = new Report { limitation = "Synchronous Editor UI checks with an injected temporary activity store and a browser-launch tripwire. Social screens are local samples with no account connection or delivery. No actual browser launch, service login, phone pairing, call, microphone, cloud request, headset input, or Android storage is tested." };
        string root = CreateTempRoot();
        var isolatedStore = new QuadLocalNotificationStore(Path.Combine(root, "ui-activity"));
        var screen = shell.FeatureScreens;
        bool wasVisible = shell.IsVisible;
        int previousPage = shell.CurrentPage;
        string previousDetail = shell.CurrentDetail;
        IDisposable injectedStore = null;
        var requestedUrls = new List<string>();
        try
        {
            injectedStore = RayBanLocalNotifications.UseStoreForEditorChecks(isolatedStore);
            screen.ExternalBrowserOpenForEditorChecks = url => requestedUrls.Add(url);
            screen.UpgradeLocalFeatures();
            screen.EnsureExternalAppScreens();
            screen.EnsureLocalNotificationScreens();
            shell.EnsureLocalNotificationDashboard();
            shell.RefreshLocalNotifications(true);
            shell.ShowLauncher();
            int controlCount = screen.Controls.Count;
            screen.EnsureExternalAppScreens();
            screen.EnsureLocalNotificationScreens();
            Assert(report, screen.Controls.Count == controlCount, "Repeated feature upgrades do not duplicate controls");

            foreach (var target in RayBanExternalAppCatalog.Targets)
            {
                string expectedView = target.AppName == "Instagram" ? "instagram" : target.AppName == "Calls" ? "calls" : "messages";
                shell.OpenApp(target.AppName);
                Assert(report, screen.CurrentView == expectedView, target.AppName + " opens an in-app sample screen");
                Assert(report, ActiveText(screen).Any(t => t.name == "Demo badge" && t.text == "SAMPLE"),
                    target.AppName + " visibly identifies its sample content");
                Assert(report, ActiveText(screen).Any(t => t.text.Contains("Account not connected")),
                    target.AppName + " makes its disconnected account state explicit");
                Assert(report, !screen.Controls.Any(t => t != null && t.gameObject.activeInHierarchy &&
                    t.ActionId != null && t.ActionId.StartsWith(RayBanExternalAppCatalog.OpenActionPrefix, StringComparison.Ordinal)),
                    target.AppName + " has no active external-browser control");
                CheckText(screen, report, target.AppName + " in-app sample");

                // Old authored buttons must remain inert, including stale UnityEvent dispatch.
                foreach (var legacyButton in screen.Controls.Where(t => t != null &&
                    t.ActionId == RayBanExternalAppCatalog.OpenActionPrefix + target.ViewKey))
                {
                    Assert(report, !legacyButton.gameObject.activeInHierarchy && !legacyButton.IsInteractable(),
                        target.AppName + " legacy browser control is hidden and disabled");
                    screen.Activate(legacyButton);
                }
                // Exercise the stale action through a real active registered tile so the
                // result is not explained solely by the inactive-control guard.
                var active = screen.Controls.First(t => t != null && t.gameObject.activeInHierarchy && t.IsInteractable() && t.ActionId != "back");
                string originalAction = active.ActionId;
                try
                {
                    active.ActionId = RayBanExternalAppCatalog.OpenActionPrefix + target.ViewKey;
                    screen.Activate(active);
                    active.ActionId = RayBanExternalAppCatalog.OpenActionPrefix + "https://example.invalid/untrusted";
                    screen.Activate(active);
                    Assert(report, requestedUrls.Count == 0 && screen.CurrentView == expectedView,
                        target.AppName + " rejects both known and arbitrary legacy browser actions");
                }
                finally { active.ActionId = originalAction; }
                screen.Open(target.ViewKey);
                Assert(report, screen.CurrentView == expectedView && requestedUrls.Count == 0,
                    target.AppName + " legacy route migrates to the in-app sample without a browser");
                Assert(report, VoiceIdle(shell), target.AppName + " sample leaves voice services idle");
                QuadRayBanDisplayPreview.Capture(shell, Path.Combine(OutputDirectory, "inapp-" + target.AppName.ToLowerInvariant() + ".png"));
                shell.Back();
                Assert(report, string.IsNullOrEmpty(shell.CurrentDetail), target.AppName + " sample Back returns to launcher");
            }

            shell.OpenApp("Messages");
            Press(screen, "comm:message-0");
            Assert(report, screen.CurrentView == "message-thread", "Sample conversation opens in-app message history");
            string initialReply = TextNamed(screen, "Reply state").text;
            Press(screen, "comm:message-reply");
            Assert(report, TextNamed(screen, "Reply state").text != initialReply &&
                (TextNamed(screen, "Reply state").text.Contains("Not sent") || TextNamed(screen, "Reply state").text.Contains("No messages sent")),
                "Sample reply updates local preview while explicitly denying delivery");
            Assert(report, VoiceIdle(shell) && requestedUrls.Count == 0 && isolatedStore.Items.Count == 0,
                "Sample reply starts no voice session, browser handoff or real app activity");
            CheckText(screen, report, "In-app sample message thread");
            QuadRayBanDisplayPreview.Capture(shell, Path.Combine(OutputDirectory, "inapp-message-thread.png"));
            Press(screen, "comm:message-reply");
            Assert(report, TextNamed(screen, "Reply state").text == initialReply, "Sample reply toggles back to its original state");
            screen.Back();
            Assert(report, screen.CurrentView == "messages", "Thread Back returns to the sample inbox");
            shell.Back();

            shell.OpenApp("Calls");
            Press(screen, "comm:call-0");
            Assert(report, screen.CurrentView == "call-preview" && TextNamed(screen, "Call state").text.Contains("No connection") &&
                TextNamed(screen, "Call preview note").text.Contains("Microphone and calling are inactive"),
                "Call preview visibly reports no call connection or microphone capture");
            string initialCall = TextNamed(screen, "Call state").text;
            Press(screen, "comm:call-mute");
            Assert(report, TextNamed(screen, "Call state").text != initialCall && VoiceIdle(shell), "Mute preview changes only the local sample state");
            CheckText(screen, report, "In-app sample call preview");
            QuadRayBanDisplayPreview.Capture(shell, Path.Combine(OutputDirectory, "inapp-call-preview.png"));
            Press(screen, "comm:call-mute");
            Press(screen, "comm:call-end");
            Assert(report, screen.CurrentView == "calls" && requestedUrls.Count == 0, "Ending a call preview returns to its in-app list");
            shell.Back();

            shell.OpenApp("Instagram");
            var like = ActiveControl(screen, "comm:instagram-like");
            string initialLike = like.Title;
            Press(screen, "comm:instagram-like");
            Assert(report, like.Title != initialLike && TextNamed(screen, "Post caption").text.Contains("Account not connected"),
                "Sample Instagram like updates locally while retaining disconnected status");
            Press(screen, "comm:instagram-like");
            Assert(report, like.Title == initialLike, "Sample Instagram like restores its initial state");
            string firstPost = TextNamed(screen, "Post number").text;
            Press(screen, "comm:instagram-next");
            Assert(report, TextNamed(screen, "Post number").text != firstPost, "Next sample Instagram post stays within the app");
            Press(screen, "comm:instagram-next");
            Press(screen, "comm:instagram-next");
            Assert(report, TextNamed(screen, "Post number").text == firstPost && VoiceIdle(shell) && requestedUrls.Count == 0,
                "Sample feed cycles locally with idle voice and no browser request");
            shell.Back();

            shell.OpenApp("Notifications");
            screen.RefreshLocalNotificationView(true);
            Assert(report, screen.CurrentView == "notifications" && TextNamed(screen, "Empty activity").gameObject.activeInHierarchy,
                "Empty local activity inbox is visible without sample contacts");
            Assert(report, !ActiveControl(screen, "activity.clear").IsInteractable(), "Empty inbox disables Clear history");
            CheckText(screen, report, "Empty activity inbox");
            string[] ids = new string[6];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = isolatedStore.Publish(i == 5 ? QuadNotificationKind.RouteArrived : QuadNotificationKind.PhotoSaved,
                    i == 5 ? "Destination reached" : "Photo saved " + (i + 1),
                    i == 5 ? "Arrived at a virtual Quad landmark." : "Your capture is available in Photos.");
            shell.SetPage(1);
            shell.RefreshLocalNotifications(true);
            Assert(report, shell.LocalNotificationAttentionVisible && isolatedStore.UnreadCount == 6,
                "Dashboard shows attention for unread local activity when DND is off");
            Assert(report, shell.VisualRoot.GetComponentsInChildren<TMP_Text>().Count(t => t.name == "Activity title") == 4,
                "Dashboard shows the four most recent real local events");
            Canvas.ForceUpdateCanvases();
            var dashboardTitles = shell.VisualRoot.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "Activity title").ToArray();
            foreach (var title in dashboardTitles) title.ForceMeshUpdate();
            Assert(report, dashboardTitles.All(t => !t.isTextOverflowing && t.textInfo.characterInfo.Take(t.textInfo.characterCount).Any(c => c.isVisible)),
                "Dashboard activity titles fit and produce visible glyphs");
            QuadRayBanDisplayPreview.Capture(shell, Path.Combine(OutputDirectory, "inapp-local-activity-dashboard.png"));
            shell.OpenApp("Notifications");
            screen.RefreshLocalNotificationView(true);
            Assert(report, ActiveText(screen).Count(t => t.name == "Activity title") == 4 && TextNamed(screen, "Activity page").text == "1 / 2",
                "Activity inbox shows four newest entries per page");
            Assert(report, TextNamed(screen, "Activity status").text.Contains("6 unread"), "Inbox reports local unread count");
            CheckText(screen, report, "Populated activity inbox");
            QuadRayBanDisplayPreview.Capture(shell, Path.Combine(OutputDirectory, "inapp-local-activity-inbox.png"));
            PressFocused(screen, "activity.next");
            Assert(report, TextNamed(screen, "Activity page").text == "2 / 2" && ActiveText(screen).Count(t => t.name == "Activity title") == 2,
                "Next activity page shows remaining entries");
            CheckPagingFocus(screen, report, "Next at last activity page");
            PressFocused(screen, "activity.previous");
            CheckPagingFocus(screen, report, "Previous at first activity page");
            Press(screen, "activity.open.0");
            Assert(report, screen.CurrentView == "notifications.detail" && isolatedStore.Find(ids[5]).IsRead && isolatedStore.UnreadCount == 5,
                "Opening newest activity marks exactly that entry read");
            Assert(report, TextNamed(screen, "Activity heading").text == "Destination reached", "Activity detail shows the selected local event");
            CheckText(screen, report, "Activity detail");
            screen.Back();
            Assert(report, screen.CurrentView == "notifications", "Activity detail Back returns to inbox");
            Press(screen, "activity.open.0");
            Press(screen, "activity.delete");
            Assert(report, screen.CurrentView == "notifications" && isolatedStore.Find(ids[5]) == null && isolatedStore.Items.Count == 5,
                "Delete removes only the selected activity entry");

            Press(screen, "activity.settings");
            Assert(report, screen.CurrentView == "do not disturb" && !isolatedStore.DoNotDisturb, "Quiet mode opens with the local DND setting");
            Press(screen, "activity.dnd.toggle");
            Assert(report, isolatedStore.DoNotDisturb && isolatedStore.Items.Count == 5 &&
                TextNamed(screen, "Quiet activity status").text == "App quiet mode is on", "DND toggles on without deleting activity");
            CheckText(screen, report, "Do not disturb");
            QuadRayBanDisplayPreview.Capture(shell, Path.Combine(OutputDirectory, "inapp-local-do-not-disturb.png"));
            Assert(report, new QuadLocalNotificationStore(Path.Combine(root, "ui-activity")).DoNotDisturb,
                "UI DND change persists in the isolated store");
            shell.SetPage(1);
            shell.RefreshLocalNotifications(true);
            Assert(report, !shell.LocalNotificationAttentionVisible && isolatedStore.UnreadCount == 5 && isolatedStore.Items.Count == 5,
                "DND suppresses dashboard attention while preserving unread history");
            shell.OpenApp("Do not disturb");
            Press(screen, "activity.inbox");
            Assert(report, TextNamed(screen, "Activity status").text.Contains("Quiet mode on"), "Inbox describes retained history while DND is on");
            Press(screen, "activity.clear");
            Assert(report, screen.CurrentView == "notifications.clear" && isolatedStore.Items.Count == 5, "Clear history requires confirmation");
            CheckText(screen, report, "Clear history confirmation");
            Press(screen, "activity.inbox");
            Assert(report, screen.CurrentView == "notifications" && isolatedStore.Items.Count == 5, "Keep cancels clearing history");
            Press(screen, "activity.clear");
            Press(screen, "activity.clear.confirm");
            Assert(report, screen.CurrentView == "notifications" && isolatedStore.Items.Count == 0 && isolatedStore.DoNotDisturb,
                "Confirmed clear removes local activity and retains quiet mode");
            shell.OpenApp("Do not disturb");
            Press(screen, "activity.dnd.toggle");
            Assert(report, !isolatedStore.DoNotDisturb, "DND can be turned off again");
            Assert(report, VoiceIdle(shell), "In-app UI checks leave microphone bridges idle");
            Assert(report, requestedUrls.Count == 0, "No browser launch was requested by any current or legacy control");
        }
        catch (Exception exception)
        {
            report.error = exception.GetType().Name + ": " + exception.Message;
            Assert(report, false, "UI harness completed without exception");
        }
        finally
        {
            screen.ExternalBrowserOpenForEditorChecks = null;
            injectedStore?.Dispose();
            try
            {
                shell.SetPage(previousPage);
                if (!string.IsNullOrEmpty(previousDetail)) shell.OpenApp(previousDetail);
                screen.RefreshLocalNotificationView(true);
                shell.RefreshLocalNotifications(true);
                if (!wasVisible) shell.Hide();
            }
            finally
            {
                RemoveTempRoot(root);
                Save(report, "inapp-ui-checks.json");
            }
        }
        return Summary("In-app UI checks", report);
    }

    private static bool VoiceIdle(RayBanDisplayShell shell) =>
        !shell.assistantBridge.IsBusy && !shell.assistantBridge.IsListening && !shell.captionBridge.IsBusy && !shell.captionBridge.IsListening;

    private static RayBanTile ActiveControl(RayBanFeatureScreen screen, string action) =>
        screen.Controls.First(t => t != null && t.ActionId == action && t.gameObject.activeInHierarchy);

    private static void Press(RayBanFeatureScreen screen, string action)
    {
        var control = ActiveControl(screen, action);
        if (!control.IsInteractable()) throw new InvalidOperationException("Disabled test control: " + action);
        screen.Activate(control);
    }

    private static void PressFocused(RayBanFeatureScreen screen, string action)
    {
        if (EventSystem.current == null) throw new InvalidOperationException("Focused UI checks require the existing EventSystem.");
        var control = ActiveControl(screen, action);
        if (!control.IsInteractable()) throw new InvalidOperationException("Disabled test control: " + action);
        // Real focus selection is essential here: uGUI clears it when the
        // selected paging button becomes non-interactable at a page boundary.
        EventSystem.current.SetSelectedGameObject(control.gameObject);
        screen.Activate(control);
    }

    private static void CheckPagingFocus(RayBanFeatureScreen screen, Report report, string context)
    {
        var selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var selected = selectedObject != null ? selectedObject.GetComponent<RayBanTile>() : null;
        bool usable = selected != null && selected.gameObject.activeInHierarchy && selected.IsInteractable();
        Assert(report, usable, context + " retains an active, interactable focus target");
        if (!usable) return;
        screen.Navigate(MoveDirection.Down);
        var movedObject = EventSystem.current.currentSelectedGameObject;
        var moved = movedObject != null ? movedObject.GetComponent<RayBanTile>() : null;
        Assert(report, moved != null && moved != selected && moved.gameObject.activeInHierarchy && moved.IsInteractable(),
            context + " permits the next directional navigation action");
    }

    private static TMP_Text[] ActiveText(RayBanFeatureScreen screen) =>
        screen.GetComponentsInChildren<TMP_Text>().Where(t => t.isActiveAndEnabled).ToArray();

    private static TMP_Text TextNamed(RayBanFeatureScreen screen, string name) => ActiveText(screen).First(t => t.name == name);

    private static void CheckText(RayBanFeatureScreen screen, Report report, string context)
    {
        Canvas.ForceUpdateCanvases();
        var texts = ActiveText(screen);
        foreach (var text in texts) text.ForceMeshUpdate();
        string[] overflow = texts.Where(t => t.isTextOverflowing).Select(t => t.name).ToArray();
        Assert(report, overflow.Length == 0, context + " text fits" + (overflow.Length > 0 ? ": " + string.Join(", ", overflow) : ""));
    }

    private static void CheckInvalidSavedStates(string root, Report report)
    {
        string malformed = Path.Combine(root, "malformed");
        Directory.CreateDirectory(malformed);
        File.WriteAllText(Path.Combine(malformed, QuadLocalNotificationStore.StorageFileName), "{ definitely not json");
        var store = new QuadLocalNotificationStore(malformed);
        Assert(report, store.Items.Count == 0 && !string.IsNullOrEmpty(store.LastStorageError), "Malformed saved activity is rejected safely");
        string oversize = Path.Combine(root, "oversize");
        Directory.CreateDirectory(oversize);
        File.WriteAllText(Path.Combine(oversize, QuadLocalNotificationStore.StorageFileName), new string('x', QuadLocalNotificationStore.MaximumFileBytes + 1));
        store = new QuadLocalNotificationStore(oversize);
        Assert(report, store.Items.Count == 0 && !string.IsNullOrEmpty(store.LastStorageError), "Oversized activity file is rejected before deserialization");

        string filtered = Path.Combine(root, "restricted-kinds");
        Directory.CreateDirectory(filtered);
        string validId = Guid.NewGuid().ToString("N");
        var state = new SavedPayload();
        state.entries.Add(new SavedEntry { id = validId, kind = 1, title = "Valid capture", message = "Local entry", createdUtcTicks = DateTime.UtcNow.Ticks });
        state.entries.Add(new SavedEntry { id = Guid.NewGuid().ToString("N"), kind = 99, title = "Unknown action", message = "https://example.invalid/", createdUtcTicks = DateTime.UtcNow.Ticks });
        state.entries.Add(new SavedEntry { id = "../../invalid", kind = 1, title = "Invalid identifier", message = "Local entry", createdUtcTicks = DateTime.UtcNow.Ticks });
        state.entries.Add(new SavedEntry { id = validId, kind = 2, title = "Duplicate identifier", message = "Duplicate", createdUtcTicks = DateTime.UtcNow.Ticks });
        state.entries.Add(new SavedEntry { id = Guid.NewGuid().ToString("N"), kind = 1, title = "Bad timestamp", message = "Invalid", createdUtcTicks = -1 });
        state.entries.Add(new SavedEntry { id = Guid.NewGuid().ToString("N"), kind = 1, title = "  ", message = "Blank title", createdUtcTicks = DateTime.UtcNow.Ticks });
        File.WriteAllText(Path.Combine(filtered, QuadLocalNotificationStore.StorageFileName), JsonUtility.ToJson(state));
        store = new QuadLocalNotificationStore(filtered);
        Assert(report, store.Items.Count == 1 && store.Items[0].Id == validId && store.Items[0].Kind == QuadNotificationKind.PhotoSaved,
            "Reload filters unknown kinds, invalid and duplicate identifiers, bad timestamps and blank titles");
        state.version = 99;
        File.WriteAllText(Path.Combine(filtered, QuadLocalNotificationStore.StorageFileName), JsonUtility.ToJson(state));
        store = new QuadLocalNotificationStore(filtered);
        Assert(report, store.Items.Count == 0 && !string.IsNullOrEmpty(store.LastStorageError), "Unsupported saved-state version is rejected");
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "QuadConnectedChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void RemoveTempRoot(string root)
    {
        string full = Path.GetFullPath(root);
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("QuadConnectedChecks-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a path outside the isolated test folder.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    private static void Assert(Report report, bool passed, string name)
    {
        report.checks.Add(new Check { name = name, passed = passed });
        if (passed) report.passed++; else report.failed++;
    }

    private static void Save(Report report, string filename)
    {
        report.finishedUtc = DateTime.UtcNow.ToString("O");
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, filename), JsonUtility.ToJson(report, true));
    }

    private static string Summary(string title, Report report) => title + ": " + report.passed + " passed; " + report.failed + " failed.";
}
#endif
