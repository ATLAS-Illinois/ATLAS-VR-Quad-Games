#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises the real follower on isolated transforms, without moving a scene rig or headset.</summary>
public static class QuadDisplayFollowChecks
{
    [Serializable] private sealed class Check { public string name; public bool passed; }
    [Serializable] private sealed class Report
    {
        public string finishedUtc, error, limitation;
        public int passed, failed;
        public float deltaTime;
        public List<Check> checks = new List<Check>();
    }

    [MenuItem("Tools/Ray-Ban Display/Validate display head following")]
    public static string RunChecks()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Wait for compilation and import before checking the follower.");

        var report = new Report
        {
            deltaTime = Time.deltaTime,
            limitation = "The actual SmoothFollow.LateUpdate is invoked on two temporary transforms in an isolated preview scene. This verifies geometry and finite smoothing, not physical Quest tracking, comfort, stereo rendering or pointer input. No existing rig, scene object, camera, user preference, microphone or Play Mode state is changed."
        };
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var head = new GameObject("Follow check target") { hideFlags = HideFlags.HideAndDontSave };
            var panel = new GameObject("Follow check panel") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(head, preview);
            SceneManager.MoveGameObjectToScene(panel, preview);
            var follow = panel.AddComponent<SmoothFollow>();
            follow.enabled = false;
            follow.target = head.transform;
            follow.requireOVRHeadset = false;
            follow.distance = .37f;
            follow.heightOffset = -.26f;
            follow.localXRotationOffset = 30f;
            follow.positionSmoothTime = 0f;
            follow.rotationLerpSpeed = 0f;
            head.transform.position = new Vector3(8f, 1.7f, -12f);
            CheckFullPose(follow, report);
            CheckLegacyYaw(follow, report);
            CheckSmoothingAndMissingTarget(follow, report);
        }
        catch (Exception exception)
        {
            report.error = exception.GetType().Name + ": " + exception.Message;
            Assert(report, false, "Follow checks completed without exception");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            const string directory = "Diagnostics/2026-11-01-QuestValidation";
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "display-follow-checks.json"), JsonUtility.ToJson(report, true));
        }
        return "Display follow checks: " + report.passed + " passed, " + report.failed + " failed.";
    }

    private static void CheckFullPose(SmoothFollow follow, Report report)
    {
        follow.onlyUseYRotation = false;
        Transform head = follow.target;
        Vector3 authoredOffset = new Vector3(0f, -.26f, .37f);
        Quaternion authoredTilt = Quaternion.Euler(30f, 0f, 0f);
        float expectedDistance = authoredOffset.magnitude;
        float expectedDownAngle = Mathf.Atan2(authoredOffset.y, authoredOffset.z) * Mathf.Rad2Deg;
        foreach (float yaw in new[] { 0f, 127f, 270f })
        foreach (float pitch in new[] { -95f, -90f, -60f, 0f, 60f, 90f, 95f })
        foreach (float roll in new[] { -35f, 0f, 35f })
        {
            head.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, roll);
            Step(follow);
            string label = "pitch=" + pitch + ", yaw=" + yaw + ", roll=" + roll;
            Vector3 localOffset = head.InverseTransformDirection(follow.transform.position - head.position);
            Assert(report, Vector3.Distance(localOffset, authoredOffset) < .00002f,
                "Full-pose preserves head-relative placement: " + label);
            Assert(report, Quaternion.Angle(Quaternion.Inverse(head.rotation) * follow.transform.rotation, authoredTilt) < .08f,
                "Full-pose preserves authored local tilt: " + label);
            Assert(report, Mathf.Abs(Vector3.Distance(head.position, follow.transform.position) - expectedDistance) < .00002f &&
                Mathf.Abs(Mathf.Atan2(localOffset.y, localOffset.z) * Mathf.Rad2Deg - expectedDownAngle) < .005f,
                "Full-pose preserves viewing distance and angular offset: " + label);
            Assert(report, Finite(follow.transform.position) && Finite(follow.transform.rotation) &&
                Vector3.Dot(follow.transform.forward, follow.transform.position - head.position) > .44f,
                "Full-pose remains finite and faces the same side: " + label);
        }

        head.rotation = Quaternion.Euler(0f, 73f, 0f);
        Step(follow);
        Vector3 fullPosition = follow.transform.position;
        Quaternion fullRotation = follow.transform.rotation;
        follow.onlyUseYRotation = true;
        Step(follow);
        Assert(report, Vector3.Distance(follow.transform.position, fullPosition) < .00002f &&
            Quaternion.Angle(follow.transform.rotation, fullRotation) < .08f,
            "Neutral head pose keeps the existing distance, height, heading and 30-degree tilt");

        follow.onlyUseYRotation = false;
        bool continuous = true;
        Vector3 previousPosition = Vector3.zero;
        Quaternion previousRotation = Quaternion.identity;
        for (int pitch = -100; pitch <= 100; pitch++)
        {
            head.rotation = Quaternion.Euler(pitch, 40f, 15f);
            Step(follow);
            if (pitch > -100)
                continuous &= Vector3.Distance(previousPosition, follow.transform.position) < .009f &&
                    Quaternion.Angle(previousRotation, follow.transform.rotation) < 1.1f;
            previousPosition = follow.transform.position;
            previousRotation = follow.transform.rotation;
        }
        Assert(report, continuous, "Full-pose sweep through both vertical poles has no position jump or rotation flip");
    }

    private static void CheckLegacyYaw(SmoothFollow follow, Report report)
    {
        follow.onlyUseYRotation = true;
        Transform head = follow.target;
        foreach (float yaw in new[] { 0f, 83f, 225f })
        {
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desiredPosition = head.position + heading * Vector3.forward * .37f + Vector3.up * -.26f;
            Quaternion desiredRotation = heading * Quaternion.Euler(30f, 0f, 0f);
            foreach (float pitch in new[] { -95f, -90.1f, -90f, -89.9f, -80f, 0f, 80f, 89.9f, 90f, 90.1f, 95f })
            {
                head.rotation = heading * Quaternion.Euler(pitch, 0f, 0f);
                Step(follow);
                Assert(report, Vector3.Distance(follow.transform.position, desiredPosition) < .00002f &&
                    Quaternion.Angle(follow.transform.rotation, desiredRotation) < .08f,
                    "Legacy yaw remains stable at pitch=" + pitch + ", yaw=" + yaw);
            }
        }
    }

    private static void CheckSmoothingAndMissingTarget(SmoothFollow follow, Report report)
    {
        follow.onlyUseYRotation = false;
        Transform head = follow.target;
        head.rotation = Quaternion.identity;
        Step(follow);
        follow.positionSmoothTime = .12f;
        follow.rotationLerpSpeed = 12f;
        bool finite = true;
        for (int index = 0; index < 160; index++)
        {
            head.rotation = Quaternion.Euler(-100f + index * 1.25f, index * .5f, 25f);
            Step(follow);
            finite &= Finite(follow.transform.position) && Finite(follow.transform.rotation) &&
                Vector3.Distance(head.position, follow.transform.position) < 1f;
        }
        Assert(report, finite, "Authored smoothing stays finite during a pitch/yaw sweep");
        Vector3 beforePosition = follow.transform.position;
        Quaternion beforeRotation = follow.transform.rotation;
        follow.target = null;
        Step(follow);
        Assert(report, follow.transform.position == beforePosition && follow.transform.rotation == beforeRotation,
            "Missing target leaves the display unchanged");
    }

    private static void Step(SmoothFollow follow) => follow.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    private static void Assert(Report report, bool condition, string name)
    {
        report.checks.Add(new Check { name = name, passed = condition });
        if (condition) report.passed++; else report.failed++;
    }
}
#endif
