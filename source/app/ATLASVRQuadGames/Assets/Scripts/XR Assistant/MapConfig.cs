using UnityEngine;
using System.Collections.Generic;

// We declare the struct here so both the Editor Window and the Map Script can read it
[System.Serializable]
public struct Landmark
{
    public string name;
    public Vector3 position;
}

public class MapConfig : ScriptableObject
{
    [Header("Global Landmarks")]
    public List<Landmark> landmarks = new List<Landmark>();
}