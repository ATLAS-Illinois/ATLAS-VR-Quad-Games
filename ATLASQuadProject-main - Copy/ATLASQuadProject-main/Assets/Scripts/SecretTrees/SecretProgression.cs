using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class SecretProgression : MonoBehaviour
{
    public int thisChapter = 1;

    [SerializeField] private GameObject ch1Paper;
    [SerializeField] private GameObject ch2Table;
    [SerializeField] private EnableCollision GhostifyIlliniUnionTree;

    [SerializeField] private GameObject ch2Paper;
    [SerializeField] private EnableCollision GhostifyColonelWolfSchool;

    [SerializeField] private GameObject ch3Table;
    [SerializeField] private GameObject ch3Paper;
    [SerializeField] private PaperText3 ch3Text;

    [SerializeField] private EnableCollision GhostifyNoyes;
    [SerializeField] private GameObject noyesSensor;
    [SerializeField] private GameObject noyesOnePager;

    [SerializeField] private EnableCollision GhostifyWohlers;
    [SerializeField] private GameObject wohlersSensor;
    [SerializeField] private GameObject gooseSensor;

    [SerializeField] private GameObject insideLincolnSensor;
    [SerializeField] private GameObject insideLincolnTable;
    [SerializeField] private GameObject lincolnOnePager;

    [SerializeField] private GameObject lincolnNoseSensor;
    [SerializeField] private EnableCollision GhostifyLibrary;
    

    private void GoToLevel2()
    {
        Debug.Log("Unlocked Level 2!");
        thisChapter = 2;
        GhostifyIlliniUnionTree.DisableCollision();

        ch2Paper.SetActive(true);
    }

    private void Start()
    {
        ch2Paper.SetActive(false);   
        ch3Paper.SetActive(false);

        noyesOnePager.SetActive(false);
        wohlersSensor.SetActive(false);
        gooseSensor.SetActive(false);
        
        lincolnOnePager.SetActive(false);
        insideLincolnTable.SetActive(false);
        insideLincolnSensor.SetActive(false);
        lincolnNoseSensor.SetActive(false);
    }

    private void GoToLevel3()
    {
        Debug.Log("Unlocked Level 3!");
        thisChapter = 3;
        GhostifyColonelWolfSchool.DisableCollision();

        ch3Paper.SetActive(true);

        //// 1st scavenger hunt location has been unlocked!
        GhostifyNoyes.DisableCollision();
        //noyesOnePager.SetActive(true);
    }

    private void GoToLevel4()
    {
        Debug.Log("1st scavenger hunt found!");
        thisChapter = 4;

        ch3Text.AdvanceChecklist();
        noyesSensor.GetComponent<SensorColor>().FoundSensor();
        GhostifyWohlers.DisableCollision();
        wohlersSensor.SetActive(true);
    }

    private void GoToLevel5()
    {
        Debug.Log("2nd scavenger hunt found!");
        thisChapter = 5;

        ch3Text.AdvanceChecklist();
        wohlersSensor.GetComponent<SensorColor>().FoundSensor();
        gooseSensor.SetActive(true);
    }

    private void GoToLevel6()
    {
        Debug.Log("3rd scavenger hunt found!");
        thisChapter = 6;

        ch3Text.AdvanceChecklist();
        gooseSensor.GetComponent<SensorColor>().FoundSensor();
        lincolnOnePager.SetActive(true);
        insideLincolnSensor.SetActive(true);
        insideLincolnTable.SetActive(true);
    }

    private void GoToLevel7()
    {
        Debug.Log("Ready for the final step of your scavenger hunt?");
        thisChapter = 7;

        ch3Text.AdvanceChecklist();
        insideLincolnSensor.GetComponent<SensorColor>().FoundSensor();
        lincolnNoseSensor.SetActive(true);
    }

    private void FinishHunt()
    {
        Debug.Log("Hunt complete!");
        lincolnNoseSensor.GetComponent<SensorColor>().FoundSensor();
        thisChapter = 8;
        ch3Text.AdvanceChecklist();
        GhostifyLibrary.DisableCollision();
    }


    // Removed invitation system due to possessing an unnecessary barrier for players.
    // Player must stay on the checklist to proceed however.
    private void Update()
    {
        if (thisChapter >= 3 && ch3Paper.GetComponent<DetectIfGrabbed>().IsGrabbed && ch3Paper.GetComponent<PageNo>().pageNumber == 10)
        {
            // If the 1st condition is not met, the 2nd one is skipped.
            // This is especially relevant because distance calculations might be computationally expensive!

            if (thisChapter == 3 && Vector3.Distance(ch3Paper.transform.position, noyesSensor.transform.position) < 2f)
                GoToLevel4();
            if (thisChapter == 4 && Vector3.Distance(ch3Paper.transform.position, wohlersSensor.transform.position) < 2f)
                GoToLevel5();
            if (thisChapter == 5 && Vector3.Distance(ch3Paper.transform.position, gooseSensor.transform.position) < 2f)
                GoToLevel6();
            if (thisChapter == 6 && Vector3.Distance(ch3Paper.transform.position, insideLincolnSensor.transform.position) < 2f)
                GoToLevel7();
            if (thisChapter == 7 && Vector3.Distance(ch3Paper.transform.position, lincolnNoseSensor.transform.position) < 2f)
                FinishHunt();
        }
        
        if (thisChapter == 1 && ch1Paper.GetComponent<DetectIfGrabbed>().IsGrabbed && 
            Vector3.Distance(ch1Paper.transform.position, ch2Table.transform.position) < 20f) 
                GoToLevel2();
        if (thisChapter == 2 && ch2Paper.GetComponent<DetectIfGrabbed>().IsGrabbed &&
            Vector3.Distance(ch2Paper.transform.position, ch3Table.transform.position) < 20f)
                GoToLevel3();
    }
}
