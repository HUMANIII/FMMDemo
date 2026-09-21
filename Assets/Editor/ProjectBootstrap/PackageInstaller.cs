using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace FmvDemo.Bootstrap
{
    public static class PackageInstaller
    {
        private static AddAndRemoveRequest request;
        private static double deadline;
        public static void Install()
        {
            request = Client.AddAndRemove(new[] {
                "com.unity.addressables@2.8.1",
                "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
                "com.unity.inputsystem@1.20.0", "com.unity.ugui@2.0.0",
                "com.unity.test-framework@1.6.0"
            }, new[] { "com.unity.collab-proxy", "com.unity.multiplayer.center", "com.unity.visualscripting",
                "com.unity.timeline", "com.unity.2d.tooling", "com.unity.2d.animation", "com.unity.2d.aseprite",
                "com.unity.2d.psdimporter", "com.unity.2d.spriteshape", "com.unity.2d.tilemap.extras", "com.unity.2d.tilemap" });
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }
        private static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (request.IsCompleted && request.Status == StatusCode.Success)
            {
                Debug.Log("[FMV Packages] " + string.Join(", ", request.Result.Select(p => p.name + "@" + p.version)));
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[FMV Packages] " + (request.Error?.message ?? "Timed out"));
                EditorApplication.Exit(1);
            }
        }
    }
}
