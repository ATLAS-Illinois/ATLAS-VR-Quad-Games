using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Optional focus-navigation fallback for the Quad display. Existing ISDK ray
/// and poke interactions remain primary; gameplay face buttons are not read.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class QuadDisplayInput : MonoBehaviour
{
    public RayBanDisplayShell shell;
    public QuestDisplayUI router;
    public bool enableKeyboardNavigation = true;
    [Tooltip("Off by default: Quad already uses the sticks for locomotion. Uses the right stick for focus and right-stick click to submit.")]
    public bool enableControllerFocusNavigation;

    private EventSystem leasedEventSystem;
    private bool originalNavigationEvents;
    private float nextRepeat;
    private MoveDirection heldDirection = MoveDirection.None;

    private System.Collections.IEnumerator Start()
    {
        // Report the actual dispatcher after EventSystem has selected its module.
        // This makes a standalone input failure diagnosable without voice/image logs.
        yield return null;
        yield return null;
        var current = EventSystem.current;
        var module = current != null ? current.currentInputModule : null;
        int activeSystems = 0;
        foreach (var system in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            if (system.isActiveAndEnabled) activeSystems++;
        bool pointable = false, ray = false, poke = false;
        if (shell != null && shell.VisualRoot != null)
            foreach (var component in shell.VisualRoot.GetComponents<MonoBehaviour>())
            {
                if (component == null || !component.isActiveAndEnabled) continue;
                string type = component.GetType().Name;
                pointable |= type == "PointableCanvas";
                ray |= type == "RayInteractable";
                poke |= type == "PokeInteractable";
            }
        string state = "[Quad UI] EventSystem=" + (current != null ? current.name : "none") +
            "; module=" + (module != null ? module.GetType().Name : "none") +
            "; active systems=" + activeSystems + "; pointable/ray/poke=" + pointable + "/" + ray + "/" + poke;
        if (module == null || module.GetType().Name != "PointableCanvasModule" || activeSystems != 1)
            Debug.LogWarning(state + "; expected one Meta pointer dispatcher.", this);
        else Debug.Log(state, this);
    }

    private void Update()
    {
        if (shell == null || !shell.OwnsPreview || !shell.IsVisible || DisplayTextInputFocus.IsEditing)
        { ReleaseNavigation(); return; }

        var eventSystem = EventSystem.current;
        var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null || shell.VisualRoot == null || !selected.transform.IsChildOf(shell.VisualRoot))
        { ReleaseNavigation(); return; }

        // Avoid a second arrow/submit from a StandaloneInputModule. This lease
        // changes navigation events only, leaving pointer events working.
        if (enableKeyboardNavigation || enableControllerFocusNavigation) AcquireNavigation(eventSystem);
        else { ReleaseNavigation(); return; }

        if (enableKeyboardNavigation && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            if (!string.IsNullOrEmpty(shell.CurrentDetail)) shell.Back();
            else if (router != null) router.CloseEntireUI();
            else shell.Hide();
            return;
        }
        MoveDirection direction = ReadDirection();
        if (direction == MoveDirection.None) heldDirection = MoveDirection.None;
        else if (heldDirection != direction || Time.unscaledTime >= nextRepeat)
        {
            bool first = heldDirection != direction;
            heldDirection = direction;
            nextRepeat = Time.unscaledTime + (first ? .35f : .16f);
            shell.Navigate(direction);
        }

        bool submit = enableKeyboardNavigation && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space));
        if (enableControllerFocusNavigation) submit |= OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick);
        if (submit && EventSystem.current != null)
        {
            selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(shell.VisualRoot))
                ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }

    private MoveDirection ReadDirection()
    {
        if (enableKeyboardNavigation)
        {
            if (Input.GetKey(KeyCode.UpArrow)) return MoveDirection.Up;
            if (Input.GetKey(KeyCode.DownArrow)) return MoveDirection.Down;
            if (Input.GetKey(KeyCode.LeftArrow)) return MoveDirection.Left;
            if (Input.GetKey(KeyCode.RightArrow)) return MoveDirection.Right;
        }
        if (enableControllerFocusNavigation)
        {
            Vector2 axis = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
            if (axis.sqrMagnitude > .49f)
                return Mathf.Abs(axis.x) > Mathf.Abs(axis.y)
                    ? (axis.x > 0 ? MoveDirection.Right : MoveDirection.Left)
                    : (axis.y > 0 ? MoveDirection.Up : MoveDirection.Down);
        }
        return MoveDirection.None;
    }

    private void AcquireNavigation(EventSystem eventSystem)
    {
        if (leasedEventSystem == eventSystem) return;
        ReleaseNavigation();
        leasedEventSystem = eventSystem;
        originalNavigationEvents = eventSystem.sendNavigationEvents;
        eventSystem.sendNavigationEvents = false;
    }

    private void ReleaseNavigation()
    {
        if (leasedEventSystem != null) leasedEventSystem.sendNavigationEvents = originalNavigationEvents;
        leasedEventSystem = null;
        heldDirection = MoveDirection.None;
    }

    private void OnDisable() => ReleaseNavigation();
}
