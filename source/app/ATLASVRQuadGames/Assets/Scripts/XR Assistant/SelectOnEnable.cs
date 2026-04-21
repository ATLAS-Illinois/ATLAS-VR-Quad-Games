using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectOnEnable : MonoBehaviour
{
    [SerializeField] private Selectable target;

    void OnEnable()
    {
        if (target != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            target.Select();
        }
    }
}
