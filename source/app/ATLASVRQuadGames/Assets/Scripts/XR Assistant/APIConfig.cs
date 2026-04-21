using UnityEngine;

// We do NOT use [CreateAssetMenu] because we want our custom window to create this
// in a very specific, hidden location to prevent accidental GitHub commits.
public class APIConfig : ScriptableObject
{
    [Header("Gemini Settings")]
    public string geminiApiKey = "";

    [Header("PieSocket Settings")]
    public string pieSocketApiKey = "";
    public string pieSocketClusterId = "";

    [Header("Azure Translation")]
    public string azureSubscriptionKey = "";
    public string azureRegion = "";
}