#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rise.Editor
{
    [InitializeOnLoad]
    internal static class FunplayMcpAutoStart
    {
        private static bool attempted;

        static FunplayMcpAutoStart()
        {
            EditorApplication.delayCall += TryStart;
        }

        private static void TryStart()
        {
            if (attempted || Application.isBatchMode)
            {
                return;
            }

            attempted = true;

            try
            {
                Type rootType = Type.GetType("Funplay.Editor.DI.RootScopeServices, Funplay.Editor");
                Type serverType = Type.GetType("Funplay.Editor.MCP.Server.MCPServerService, Funplay.Editor");
                if (rootType == null || serverType == null)
                {
                    Debug.LogWarning("[Rise] Funplay MCP types were not found. Is the Funplay package loaded?");
                    return;
                }

                object services = rootType.GetProperty("Services", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (services == null)
                {
                    Debug.LogWarning("[Rise] Funplay root services are not available yet.");
                    return;
                }

                object server = ((IServiceProvider)services).GetService(serverType);
                if (server == null)
                {
                    Debug.LogWarning("[Rise] Funplay MCP server service is not available.");
                    return;
                }

                bool isRunning = (bool)(serverType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Instance)?.GetValue(server) ?? false);
                if (isRunning)
                {
                    return;
                }

                MethodInfo startAsync = serverType.GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                startAsync?.Invoke(server, null);
                Debug.Log("[Rise] Requested Funplay MCP server start.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Rise] Failed to auto-start Funplay MCP: {ex.Message}");
            }
        }
    }
}
#endif
