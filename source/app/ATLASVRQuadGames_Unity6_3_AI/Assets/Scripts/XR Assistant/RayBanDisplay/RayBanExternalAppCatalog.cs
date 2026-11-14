using System;
using System.Collections.Generic;

/// <summary>Fixed destinations only; never treats an app name or action as a URL.</summary>
public static class RayBanExternalAppCatalog
{
    public const string OpenActionPrefix = "external.open:";

    public sealed class Target
    {
        public string AppName { get; }
        public string ViewKey { get; }
        public string Url { get; }
        public string HostLabel { get; }
        public string Symbol { get; }
        public string ServiceTitle { get; }
        public string Description { get; }

        internal Target(string appName, string key, string url, string hostLabel, string symbol, string serviceTitle, string description)
        {
            AppName = appName;
            ViewKey = key;
            Url = url;
            HostLabel = hostLabel;
            Symbol = symbol;
            ServiceTitle = serviceTitle;
            Description = description;
        }
    }

    private static readonly Target[] KnownTargets = {
        new Target("Instagram", "external.instagram", "https://www.instagram.com/", "instagram.com", "instagram", "Instagram on the web",
            "Open Instagram in the browser and sign in to your account. Posts and messages stay in the browser."),
        new Target("WhatsApp", "external.whatsapp", "https://web.whatsapp.com/", "web.whatsapp.com", "whatsapp", "WhatsApp Web",
            "Open WhatsApp Web and follow its instructions to link your phone. Conversations stay in the browser."),
        new Target("Messenger", "external.messenger", "https://www.messenger.com/", "messenger.com", "messenger", "Messenger on the web",
            "Open Messenger in the browser and sign in to your account. Conversations stay in the browser."),
        new Target("Messages", "external.messages", "https://messages.google.com/web/", "messages.google.com", "messages", "Google Messages",
            "Pair Google Messages with your Android phone in the browser. Your phone needs an internet connection. Carrier charges may apply."),
        new Target("Calls", "external.calls", "https://meet.google.com/", "meet.google.com", "calls", "Internet calls with Meet",
            "Open Google Meet for internet calls. Sign-in, microphone permission and browser support may be required. This is not a phone dialer.")
    };

    public static IReadOnlyList<Target> Targets { get; } = Array.AsReadOnly(KnownTargets);

    public static bool TryGetByApp(string appName, out Target target)
    {
        foreach (var known in KnownTargets)
            if (string.Equals(appName, known.AppName, StringComparison.OrdinalIgnoreCase))
            { target = known; return true; }
        target = null;
        return false;
    }

    public static bool TryGetByKey(string key, out Target target)
    {
        foreach (var known in KnownTargets)
            if (string.Equals(key, known.ViewKey, StringComparison.Ordinal))
            { target = known; return true; }
        target = null;
        return false;
    }

    public static bool TryGetByAction(string action, out Target target)
    {
        if (action != null && action.StartsWith(OpenActionPrefix, StringComparison.Ordinal))
            return TryGetByKey(action.Substring(OpenActionPrefix.Length), out target);
        target = null;
        return false;
    }
}
