using UnityEngine;
using UnityEditor;
using System.IO;

public class APIConfigWindow : EditorWindow
{
    private APIConfig config;
    private const string configPath = "Assets/Resources/API_Secrets.asset";

    // This creates the custom menu button at the very top of Unity!
    [MenuItem("ATLAS VR/API Setup")]
    public static void ShowWindow()
    {
        GetWindow<APIConfigWindow>("API Setup");
    }

    private void OnEnable()
    {
        LoadOrCreateConfig();
    }

    private void LoadOrCreateConfig()
    {
        config = AssetDatabase.LoadAssetAtPath<APIConfig>(configPath);
        if (config == null)
        {
            // Ensure the Resources folder exists
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }

            config = ScriptableObject.CreateInstance<APIConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Project API Keys & Secrets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These keys are saved locally in a ScriptableObject and will be ignored by Git.", MessageType.Info);

        if (config == null) LoadOrCreateConfig();

        Undo.RecordObject(config, "Update API Keys");

        EditorGUILayout.Space();
        config.geminiApiKey = EditorGUILayout.TextField("Gemini API Key", config.geminiApiKey);

        EditorGUILayout.Space();
        config.pieSocketApiKey = EditorGUILayout.TextField("PieSocket API Key", config.pieSocketApiKey);
        config.pieSocketClusterId = EditorGUILayout.TextField("PieSocket Cluster ID", config.pieSocketClusterId);

        EditorGUILayout.Space();
        config.azureSubscriptionKey = EditorGUILayout.TextField("Azure Subscription Key", config.azureSubscriptionKey);
        config.azureRegion = EditorGUILayout.TextField("Azure Region", config.azureRegion);

        EditorGUILayout.Space();
        if (GUILayout.Button("Save Configurations", GUILayout.Height(30)))
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[ATLAS VR] API Keys saved successfully!");
        }
    }
}