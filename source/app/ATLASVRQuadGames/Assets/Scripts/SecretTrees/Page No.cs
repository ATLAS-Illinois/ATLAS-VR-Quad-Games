using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class PageNo : MonoBehaviour
{
    public int pageNumber = 1;
    private float buttonCooldown = 0f;
    public int minPageNo = 1;
    public int maxPageNo = 10;

    [SerializeField] private DetectIfGrabbed dig;

    // Start is called before the first frame update
    void Start()
    {   
        pageNumber = 1;
        if (minPageNo > maxPageNo || minPageNo < 1 || maxPageNo < 1)
        {
            minPageNo = 1;
            maxPageNo = 1;
            Debug.Log("Invalid min and/or max page number values. Paper is locked.");
        }
    }

    void Update()
    {
        if (buttonCooldown <= 0f)
        {
            if (!dig.IsGrabbed) return;

            // IsGrabbedLeft & IsGrabbedRight are mutually exclusive
            var hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (dig.IsGrabbedLeft)
                hand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (dig.IsGrabbedLeft && dig.IsGrabbedRight)
                    Debug.Log("Variable whether left / right hand grabs the paper is not mutually exclusive. ERROR");

            if (pageNumber < maxPageNo && hand.TryGetFeatureValue(
                CommonUsages.secondaryButton, out bool handSecondary) && handSecondary)
            {
                pageNumber++;
                //Debug.Log("Page number is: " + pageNumber);
                buttonCooldown = 0.5f;
            }
            else if (pageNumber > minPageNo && hand.TryGetFeatureValue(
                    CommonUsages.primaryButton, out bool handPrimary) && handPrimary)
            {
                pageNumber--;
                //Debug.Log("Page number is: " + pageNumber);
                buttonCooldown = 0.5f;
            }
        }
        else
        {
            buttonCooldown -= Time.deltaTime;
        }
    }
}
