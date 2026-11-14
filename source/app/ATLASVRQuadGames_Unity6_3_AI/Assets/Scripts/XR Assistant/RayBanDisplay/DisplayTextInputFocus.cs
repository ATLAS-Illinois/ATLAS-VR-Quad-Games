using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Keep debug gesture shortcuts from consuming text typed into an app input.</summary>
public static class DisplayTextInputFocus
{
    public static bool IsEditing
    {
        get
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var text = selected.GetComponent<TMP_InputField>();
            if (text != null && text.isFocused) return true;
            var legacy = selected.GetComponent<InputField>();
            return legacy != null && legacy.isFocused;
        }
    }
}
