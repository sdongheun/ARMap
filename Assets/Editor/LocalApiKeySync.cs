using System;
using System.IO;
using Google.XR.ARCoreExtensions.Internal;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LocalApiKeySync
{
    [Serializable]
    private class LocalApiKeys
    {
        public string kakaoRestApiKey;
        public string androidCloudServicesApiKey;
        public string iosCloudServicesApiKey;
    }

    static LocalApiKeySync()
    {
        ApplyGoogleApiKeys();
    }

    private static void ApplyGoogleApiKeys()
    {
        string localKeysPath = Path.Combine(Application.dataPath, "StreamingAssets/LocalApiKeys.json");
        if (!File.Exists(localKeysPath)) return;

        try
        {
            LocalApiKeys keys = JsonUtility.FromJson<LocalApiKeys>(File.ReadAllText(localKeysPath));
            if (keys == null) return;

            ARCoreExtensionsProjectSettings settings = ARCoreExtensionsProjectSettings.Instance;
            settings.AndroidCloudServicesApiKey = keys.androidCloudServicesApiKey ?? string.Empty;
            settings.IOSCloudServicesApiKey = keys.iosCloudServicesApiKey ?? string.Empty;

            settings.AndroidAuthenticationStrategySetting =
                string.IsNullOrWhiteSpace(settings.AndroidCloudServicesApiKey)
                ? AndroidAuthenticationStrategy.DoNotUse
                : AndroidAuthenticationStrategy.ApiKey;

            settings.IOSAuthenticationStrategySetting =
                string.IsNullOrWhiteSpace(settings.IOSCloudServicesApiKey)
                ? IOSAuthenticationStrategy.DoNotUse
                : IOSAuthenticationStrategy.ApiKey;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to sync local API keys: {ex.Message}");
        }
    }
}
