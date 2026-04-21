using UnityEngine;
using UnityEditor;
using System.IO;

public class MapConfigWindow : EditorWindow
{
    private MapConfig config;
    private const string configPath = "Assets/Resources/Map_Settings.asset";
    private SerializedObject serializedConfig;

    // Creates the dedicated menu button!
    [MenuItem("ATLAS VR/Landmark Registry")]
    public static void ShowWindow()
    {
        GetWindow<MapConfigWindow>("Landmarks");
    }

    private void OnEnable()
    {
        LoadOrCreateConfig();
    }

    private void LoadOrCreateConfig()
    {
        config = AssetDatabase.LoadAssetAtPath<MapConfig>(configPath);
        if (config == null)
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }

            config = ScriptableObject.CreateInstance<MapConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
        }

        // This is required to draw Unity's nice + / - List interface
        serializedConfig = new SerializedObject(config);
    }

    private void OnGUI()
    {
        if (config == null || serializedConfig == null) LoadOrCreateConfig();

        serializedConfig.Update();

        GUILayout.Label("Global Map Landmarks", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Add the XYZ coordinates for buildings on the Quad here. The VR Map will read these automatically.", MessageType.Info);

        EditorGUILayout.Space();

        // Draw the interactive list
        SerializedProperty landmarksProp = serializedConfig.FindProperty("landmarks");
        EditorGUILayout.PropertyField(landmarksProp, new GUIContent("Coordinate Registry"), true);

        serializedConfig.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Save Landmarks", GUILayout.Height(30)))
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[ATLAS VR] Landmarks saved successfully!");
        }
    }
}