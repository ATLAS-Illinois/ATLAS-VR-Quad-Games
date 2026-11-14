using System;
using System.Collections.Generic;
using UnityEngine;

public partial class RayBanDisplayShell
{
    private const string LauncherOrderPreference = "ATLASVRQuadGames.Display.LauncherOrder.v1";
    private readonly List<string> launcherOrder = new List<string>();
    private readonly int[] dashboardSelections = new int[3];
    private bool launcherOrderLoaded;
    private bool dashboardStateInitialized;
    private string selectedLauncherApp;
    private float savedLauncherScroll;

    public int LauncherAppCount => AppNames.Length;

    public string GetLauncherApp(int position)
    {
        EnsureLauncherOrder();
        return launcherOrder[Mathf.Clamp(position, 0, launcherOrder.Count - 1)];
    }

    public string GetLauncherAppSymbol(int position)
    {
        int index = Array.IndexOf(AppNames, GetLauncherApp(position));
        return index >= 0 ? AppSymbols[index] : "settings";
    }

    public bool HasCustomLauncherOrder
    {
        get
        {
            EnsureLauncherOrder();
            for (int i = 0; i < AppNames.Length; i++)
                if (launcherOrder[i] != AppNames[i]) return true;
            return false;
        }
    }

    private void EnsureLauncherOrder()
    {
        if (!launcherOrderLoaded)
        {
            launcherOrderLoaded = true;
            launcherOrder.Clear();
            // Names survive catalog additions; unknown/duplicate entries are ignored.
            string saved = Application.isPlaying ? PlayerPrefs.GetString(LauncherOrderPreference, "") : "";
            foreach (string name in saved.Split('|'))
                if (Array.IndexOf(AppNames, name) >= 0 && !launcherOrder.Contains(name)) launcherOrder.Add(name);
            foreach (string name in AppNames)
                if (!launcherOrder.Contains(name)) launcherOrder.Add(name);
        }
        ApplyLauncherOrder();
    }

    private void ApplyLauncherOrder()
    {
        // Reposition the existing tiles so their icons, callbacks and references stay intact.
        foreach (var tile in tiles)
        {
            if (tile == null || tile.PageId != 2) continue;
            int position = launcherOrder.IndexOf(tile.Title);
            if (position < 0) continue;
            tile.ItemIndex = position;
            tile.Row = position / 2;
            tile.Column = position % 2;
            var rect = (RectTransform)tile.transform;
            rect.anchoredPosition = new Vector2(16 + tile.Column * 286 + rect.sizeDelta.x * .5f,
                -tile.Row * 132 - rect.sizeDelta.y * .5f);
            tile.transform.SetSiblingIndex(position);
        }
    }

    public bool MoveLauncherApp(int from, int to)
    {
        EnsureLauncherOrder();
        if (from < 0 || from >= launcherOrder.Count || to < 0 || to >= launcherOrder.Count || from == to) return false;
        string name = launcherOrder[from];
        launcherOrder.RemoveAt(from);
        launcherOrder.Insert(to, name);
        SaveLauncherOrder();
        return true;
    }

    public void ResetLauncherOrder()
    {
        launcherOrder.Clear();
        launcherOrder.AddRange(AppNames);
        launcherOrderLoaded = true;
        SaveLauncherOrder();
    }

    private void SaveLauncherOrder()
    {
        ApplyLauncherOrder();
        if (Application.isPlaying)
        {
            PlayerPrefs.SetString(LauncherOrderPreference, string.Join("|", launcherOrder));
            PlayerPrefs.Save();
        }
        UpdateScrollFade();
    }

    private void InitializeDashboardState()
    {
        if (dashboardStateInitialized) return;
        dashboardStateInitialized = true;
        dashboardSelections[Mathf.Clamp(currentPage, 0, 2)] = selectedIndex;
        if (currentPage == 2)
        {
            var selected = FindItem(2, selectedIndex);
            selectedLauncherApp = selected != null ? selected.Title : null;
            savedLauncherScroll = appsContent != null ? appsContent.anchoredPosition.y : 0;
        }
    }

    private void CaptureDashboardState()
    {
        if (!string.IsNullOrEmpty(currentDetail)) return;
        InitializeDashboardState();
        dashboardSelections[Mathf.Clamp(currentPage, 0, 2)] = selectedIndex;
        if (currentPage != 2) return;
        var selected = FindItem(2, selectedIndex);
        selectedLauncherApp = selected != null ? selected.Title : selectedLauncherApp;
        if (appsContent != null) savedLauncherScroll = appsContent.anchoredPosition.y;
        if (appScroll != null) appScroll.StopMovement();
    }

    private void RestoreDashboardState()
    {
        InitializeDashboardState();
        selectedIndex = dashboardSelections[Mathf.Clamp(currentPage, 0, 2)];
        if (currentPage != 2) return;
        EnsureLauncherOrder();
        int namedPosition = launcherOrder.IndexOf(selectedLauncherApp ?? "");
        if (namedPosition >= 0) selectedIndex = namedPosition;
        if (appsContent == null || appsViewport == null) return;
        if (appScroll != null) appScroll.StopMovement();
        float limit = Mathf.Max(0, appsContent.rect.height - appsViewport.rect.height);
        appsContent.anchoredPosition = new Vector2(0, Mathf.Clamp(savedLauncherScroll, 0, limit));
    }
}
