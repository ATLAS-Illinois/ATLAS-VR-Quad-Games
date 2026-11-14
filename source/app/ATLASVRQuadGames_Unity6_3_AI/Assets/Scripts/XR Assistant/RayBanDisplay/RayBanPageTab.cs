using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pointer access to the display's three dashboard pages.</summary>
public sealed class RayBanPageTab : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public RayBanDisplayShell shell;
    [Range(0, 2)] public int page;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Open();
    }
    public void OnSubmit(BaseEventData eventData) => Open();
    private void Open() { if (shell != null) shell.SetPage(page); }
}
