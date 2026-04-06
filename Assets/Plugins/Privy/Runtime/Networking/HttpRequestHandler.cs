using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Privy
{
    internal class HttpRequestHandler : IHttpRequestHandler
    {
        private const string DefaultEditorOrigin = "http://localhost:8080";
        private string _baseUrl;
        private string _appId;
        private string _clientId;
        private string _clientAnalyticsId;
        private string _appIdentifier;
        private IClientAnalyticsIdRepository _clientAnalyticsIdRepository;
        private static readonly string ContentType = "application/json";

        public HttpRequestHandler(
            PrivyConfig privyConfig,
            IClientAnalyticsIdRepository clientAnalyticsIdRepository
        )
        {
            _appId = privyConfig.AppId;
            _clientId = privyConfig.ClientId;
            _baseUrl = $"{PrivyEnvironment.BASE_URL}/api/v1";
            _clientAnalyticsIdRepository = clientAnalyticsIdRepository;
            _appIdentifier = Application.identifier;

            PrivyLogger.Debug($"App identifier is {_appId}");
            PrivyLogger.Debug($"Unity app identifier is {_appIdentifier}");
            PrivyLogger.Debug($"Client app identifier is {_clientId}");

            PrivyLogger.Internal($"SDK version is {SdkVersion.VersionNumber}");
        }

        // Method to send HTTP requests
        public async Task<string> SendRequestAsync(string path, string jsonData,
            Dictionary<string, string> customHeaders = null, string method = "POST")
        {
            PrivyLogger.Debug("Logging in SendRequestAsync");
            var endpoint = GetFullUrl(path); //need to be careful here, to ensure no issues with slashes
            using (UnityWebRequest request = new UnityWebRequest(endpoint, method))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", ContentType);

                // app id header
                request.SetRequestHeader(Constants.PRIVY_APP_ID_HEADER, _appId);

                // client header
                request.SetRequestHeader(Constants.PRIVY_CLIENT_HEADER, $"unity:{SdkVersion.VersionNumber}");

                // client id header
                request.SetRequestHeader(Constants.PRIVY_CLIENT_ID_HEADER, _clientId);

                // Analytics client header
                string clientAnalyticsId = _clientAnalyticsIdRepository.LoadClientId();
                request.SetRequestHeader(Constants.PRIVY_CLIENT_ANALYTICS_ID_HEADER, clientAnalyticsId);

                // Only send native app identifier on actual native builds (Android/iOS).
                bool isNativeBuild = Application.platform == RuntimePlatform.Android
                    || Application.platform == RuntimePlatform.IPhonePlayer;
                if (_appIdentifier != null && isNativeBuild)
                {
                    request.SetRequestHeader(Constants.PRIVY_NATIVE_APP_IDENTIFIER, _appIdentifier);
                }

                bool originApplied = false;
                string originUsed = string.Empty;

                // Unity Editor/desktop requests are not browser-originated and therefore don't include Origin.
                // Privy requires an Origin header for these requests.
                // NOTE: In Editor with WebGL build target, UNITY_WEBGL can be defined.
                // Keep Editor path enabled so Play Mode auth still sends Origin.
#if UNITY_EDITOR || !UNITY_WEBGL
                bool requiresManualOrigin = Application.isEditor
                    || (!isNativeBuild && Application.platform != RuntimePlatform.WebGLPlayer);
                if (requiresManualOrigin)
                {
                    string origin = GetEditorOrigin();
                    if (!string.IsNullOrEmpty(origin))
                    {
                        request.SetRequestHeader("Origin", origin);
                        originApplied = true;
                        originUsed = origin;
                    }
                }
#endif

                if ((Application.isEditor || Debug.isDebugBuild) && ShouldLogOriginDebugForPath(path))
                {
                    Debug.Log(
                        $"[PrivyAuth][HttpRequestHandler] platform={Application.platform}, isEditor={Application.isEditor}, isNativeBuild={isNativeBuild}, originApplied={originApplied}, origin={(string.IsNullOrEmpty(originUsed) ? "(none)" : originUsed)}, path={path}"
                    );
                }

                if (customHeaders != null)
                {
                    foreach (var header in customHeaders)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }

                PrivyLogger.Internal($"Firing HTTP request to: {endpoint}");
                PrivyLogger.Internal($"HTTP request body {jsonData}");

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Deserialize JSON response into TResponse
                    return request.downloadHandler.text;
                }
                else
                {
                    int statusCode = (int)request.responseCode;
                    string errorMessage = $"HTTP request failed (status {statusCode}): {request.error}";

                    if (request.downloadHandler != null)
                    {
                        string responseBody = request.downloadHandler.text;
                        errorMessage += $" Response Body: {responseBody}";
                    }

                    throw new Exception(errorMessage);
                }
            }
        }

        public string GetFullUrl(string path) => $"{_baseUrl}/{path}";

        private static string GetEditorOrigin()
        {
            // Optional override to match the Privy dashboard allowlist.
            string overrideOrigin = Environment.GetEnvironmentVariable("PRIVY_ORIGIN");
            if (!string.IsNullOrEmpty(overrideOrigin))
            {
                return overrideOrigin;
            }

            return DefaultEditorOrigin;
        }

        private static bool ShouldLogOriginDebugForPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Keep only auth + wallet critical path logs to reduce noise.
            return path.Contains("passwordless")
                || path.Contains("sessions")
                || path.Contains("wallets")
                || path.Contains("apps/");
        }

    }
}