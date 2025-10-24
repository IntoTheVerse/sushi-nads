using System;
using UnityEngine;
using Privy;

/// <summary>
/// Helper to extract Privy authentication token
/// This token is needed for backend API authentication
/// </summary>
public static class PrivyTokenHelper
{
    /// <summary>
    /// Get Privy auth token for backend API calls
    /// Returns null if not authenticated or token unavailable
    /// </summary>
    public static string GetAuthToken()
    {
        Debug.Log("🔍 Starting token search...");
        
        try
        {
            if (PrivyManager.Instance == null)
            {
                Debug.LogError("❌ PrivyManager.Instance is NULL!");
                return null;
            }
            
            Debug.Log("✅ PrivyManager.Instance exists");
            
            if (PrivyManager.Instance.User == null)
            {
                Debug.LogError("❌ PrivyManager.Instance.User is NULL!");
                return null;
            }
            
            Debug.Log("✅ User exists");

            var user = PrivyManager.Instance.User;

            // ============================================
            // CHECK PRIVYMANAGER FIRST (likely location)
            // ============================================
            
            Debug.Log("🔍 === Checking PrivyManager for token ===");
            var managerType = PrivyManager.Instance.GetType();
            var managerProps = managerType.GetProperties();
            Debug.Log($"Found {managerProps.Length} properties on PrivyManager");
            
            foreach (var prop in managerProps)
            {
                try
                {
                    var value = prop.GetValue(PrivyManager.Instance);
                    Debug.Log($"PrivyManager.{prop.Name}: {value?.GetType().Name ?? "null"}");
                    
                    // Look for token-like properties
                    if (prop.Name.ToLower().Contains("token") && value != null)
                    {
                        string tokenValue = value.ToString();
                        if (!string.IsNullOrEmpty(tokenValue) && tokenValue.Length > 20 && tokenValue.StartsWith("eyJ"))
                        {
                            string preview = tokenValue.Length > 30 
                                ? $"{tokenValue.Substring(0, 30)}...{tokenValue.Substring(tokenValue.Length - 10)}"
                                : tokenValue;
                            Debug.Log($"🎉🔑 FOUND AUTH TOKEN on PrivyManager.{prop.Name}!");
                            Debug.Log($"🔑 Token preview: {preview}");
                            Debug.Log($"🔑 Token length: {tokenValue.Length} chars");
                            return tokenValue;
                        }
                    }
                }
                catch { }
            }

            // ============================================
            // CHECK AUTHSTATE (likely location for token!)
            // ============================================
            Debug.Log("🔍 === Checking AuthState for token ===");
            var authState = PrivyManager.Instance.AuthState;
            if (authState != null)
            {
                var authStateType = authState.GetType();
                var authStateProps = authStateType.GetProperties();
                
                Debug.Log($"Found {authStateProps.Length} properties on AuthState");
                
                foreach (var prop in authStateProps)
                {
                    try
                    {
                        var value = prop.GetValue(authState);
                        Debug.Log($"AuthState.{prop.Name}: {value?.ToString() ?? "null"}");
                        
                        // Look for token
                        if ((prop.Name.ToLower().Contains("token") || prop.Name.ToLower().Contains("access")) && value != null)
                        {
                            string tokenValue = value.ToString();
                            if (!string.IsNullOrEmpty(tokenValue) && tokenValue.Length > 20 && tokenValue.StartsWith("eyJ"))
                            {
                                string preview = tokenValue.Length > 30 
                                    ? $"{tokenValue.Substring(0, 30)}...{tokenValue.Substring(tokenValue.Length - 10)}"
                                    : tokenValue;
                                Debug.Log($"🎉🔑 FOUND AUTH TOKEN on AuthState.{prop.Name}!");
                                Debug.Log($"🔑 Token preview: {preview}");
                                Debug.Log($"🔑 Token length: {tokenValue.Length} chars");
                                return tokenValue;
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                Debug.LogWarning("⚠️ AuthState is null");
            }

            // ============================================
            // DEBUG: Print available properties on User
            // ============================================
            Debug.Log("=== Privy User Object Debug ===");
            Debug.Log($"User ID: {user.Id}");
            Debug.Log($"User Type: {user.GetType().Name}");
            
            // Use reflection to find token properties
            var userType = user.GetType();
            var properties = userType.GetProperties();
            
            Debug.Log("Available properties on User:");
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(user);
                    Debug.Log($"  {prop.Name}: {value?.ToString() ?? "null"}");
                    
                    // Look for token-like properties
                    if (prop.Name.ToLower().Contains("token") && value != null)
                    {
                        string tokenValue = value.ToString();
                        if (!string.IsNullOrEmpty(tokenValue) && tokenValue.Length > 20)
                        {
                            string preview = tokenValue.Length > 30 
                                ? $"{tokenValue.Substring(0, 30)}...{tokenValue.Substring(tokenValue.Length - 10)}"
                                : tokenValue;
                            Debug.Log($"🎉🔑 FOUND AUTH TOKEN on User.{prop.Name}!");
                            Debug.Log($"🔑 Token preview: {preview}");
                            Debug.Log($"🔑 Token length: {tokenValue.Length} chars");
                            return tokenValue;
                        }
                    }
                }
                catch { }
            }
            
            Debug.LogWarning("⚠️ Could not find auth token property. Check console output above.");
            Debug.LogWarning("Update PrivyTokenHelper.cs with the correct property name.");
            
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ EXCEPTION in GetAuthToken: {e.GetType().Name}");
            Debug.LogError($"❌ Message: {e.Message}");
            Debug.LogError($"❌ Stack: {e.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Save token to PlayerPrefs for API use
    /// </summary>
    public static void SaveToken()
    {
        string token = GetAuthToken();
        
        if (!string.IsNullOrEmpty(token))
        {
            PlayerPrefs.SetString("privyAuthToken", token);
            PlayerPrefs.Save();
            
            // Only log first/last few chars for security
            string preview = token.Length > 20 
                ? $"{token.Substring(0, 10)}...{token.Substring(token.Length - 10)}"
                : "***";
                
            Debug.Log($"💾✅ PRIVY TOKEN SAVED SUCCESSFULLY!");
            Debug.Log($"🔑 Preview: {preview}");
            Debug.Log($"📏 Length: {token.Length} characters");
        }
        else
        {
            Debug.LogError("❌🚨 FAILED TO SAVE TOKEN - No token found!");
        }
    }

    /// <summary>
    /// Get saved token from PlayerPrefs
    /// </summary>
    public static string GetSavedToken()
    {
        return PlayerPrefs.GetString("privyAuthToken", "");
    }

    /// <summary>
    /// Clear saved token (on logout)
    /// </summary>
    public static void ClearToken()
    {
        PlayerPrefs.DeleteKey("privyAuthToken");
        PlayerPrefs.Save();
        Debug.Log("✅ Token cleared");
    }
}

