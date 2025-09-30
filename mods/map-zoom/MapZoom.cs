using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.MapZoom
{
    [BepInPlugin("com.yourname.mapzoom", "Map Zoom", "1.0.0")]
    public class MapZoomPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<float> configMaxZoom;
        private static ConfigEntry<float> configMinZoom;
        private static ConfigEntry<float> configZoomSpeed;
        private static ConfigEntry<bool> configSmoothZoom;
        private static ConfigEntry<bool> configZoomToFit;
        private static ConfigEntry<KeyCode> configZoomInKey;
        private static ConfigEntry<KeyCode> configZoomOutKey;
        private static ConfigEntry<KeyCode> configResetZoomKey;
        private static ConfigEntry<KeyCode> configFitToMapKey;
        
        private static Camera mainCamera;
        private static float originalSize;
        private static float currentZoom = 1f;
        private static bool isZooming = false;
        private static Vector3 originalPosition;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable map zoom functionality");
            configMaxZoom = Config.Bind("Zoom", "MaxZoom", 0.1f, "Maximum zoom out level (smaller = more zoomed out)");
            configMinZoom = Config.Bind("Zoom", "MinZoom", 1f, "Minimum zoom level (normal zoom)");
            configZoomSpeed = Config.Bind("Zoom", "ZoomSpeed", 2f, "Zoom speed multiplier");
            configSmoothZoom = Config.Bind("Zoom", "SmoothZoom", true, "Enable smooth zoom transitions");
            configZoomToFit = Config.Bind("Zoom", "ZoomToFit", true, "Auto-zoom to fit entire map");
            configZoomInKey = Config.Bind("Controls", "ZoomInKey", KeyCode.Equals, "Key to zoom in");
            configZoomOutKey = Config.Bind("Controls", "ZoomOutKey", KeyCode.Minus, "Key to zoom out");
            configResetZoomKey = Config.Bind("Controls", "ResetZoomKey", KeyCode.R, "Key to reset zoom");
            configFitToMapKey = Config.Bind("Controls", "FitToMapKey", KeyCode.F, "Key to fit entire map");
            
            var harmony = new Harmony("com.yourname.mapzoom");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("MapZoom", "Map Zoom mod loaded successfully!");
        }

        [HarmonyPatch(typeof(Camera), nameof(Camera.Start))]
        public static class CameraStartPatch
        {
            public static void Postfix(Camera __instance)
            {
                if (!configEnabled.Value) return;
                
                InitializeCamera(__instance);
            }
        }

        private static void InitializeCamera(Camera camera)
        {
            if (camera.name == "Main Camera")
            {
                mainCamera = camera;
                originalSize = camera.orthographicSize;
                originalPosition = camera.transform.position;
                
                CommonUtilities.LogMessage("MapZoom", "Camera initialized for zoom functionality");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class PlayerUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value || mainCamera == null) return;
                
                HandleZoomInput();
                UpdateCameraZoom();
            }
        }

        private static void HandleZoomInput()
        {
            // Zoom in
            if (Input.GetKey(configZoomInKey.Value))
            {
                ZoomIn();
            }
            
            // Zoom out
            if (Input.GetKey(configZoomOutKey.Value))
            {
                ZoomOut();
            }
            
            // Reset zoom
            if (Input.GetKeyDown(configResetZoomKey.Value))
            {
                ResetZoom();
            }
            
            // Fit to map
            if (Input.GetKeyDown(configFitToMapKey.Value))
            {
                FitToMap();
            }
            
            // Mouse wheel zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                if (scroll > 0f)
                {
                    ZoomIn();
                }
                else
                {
                    ZoomOut();
                }
            }
        }

        private static void ZoomIn()
        {
            float newZoom = currentZoom * (1f + configZoomSpeed.Value * Time.deltaTime);
            newZoom = Mathf.Clamp(newZoom, configMaxZoom.Value, configMinZoom.Value);
            SetZoom(newZoom);
        }

        private static void ZoomOut()
        {
            float newZoom = currentZoom / (1f + configZoomSpeed.Value * Time.deltaTime);
            newZoom = Mathf.Clamp(newZoom, configMaxZoom.Value, configMinZoom.Value);
            SetZoom(newZoom);
        }

        private static void SetZoom(float zoomLevel)
        {
            currentZoom = zoomLevel;
            isZooming = true;
            
            if (configSmoothZoom.Value)
            {
                // Smooth zoom transition
                StartCoroutine(SmoothZoomCoroutine());
            }
            else
            {
                // Instant zoom
                ApplyZoom();
            }
        }

        private static System.Collections.IEnumerator SmoothZoomCoroutine()
        {
            float startSize = mainCamera.orthographicSize;
            float targetSize = originalSize * currentZoom;
            float duration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
                yield return null;
            }
            
            ApplyZoom();
            isZooming = false;
        }

        private static void ApplyZoom()
        {
            if (mainCamera == null) return;
            
            mainCamera.orthographicSize = originalSize * currentZoom;
            
            // Center camera on map if zoomed out significantly
            if (currentZoom < 0.5f)
            {
                CenterCameraOnMap();
            }
        }

        private static void CenterCameraOnMap()
        {
            if (mainCamera == null) return;
            
            // Get map bounds
            var mapBounds = GetMapBounds();
            if (mapBounds != null)
            {
                Vector3 center = mapBounds.Value.center;
                center.z = mainCamera.transform.position.z; // Keep original Z position
                mainCamera.transform.position = center;
            }
        }

        private static Bounds? GetMapBounds()
        {
            // Try to find map boundaries
            var mapObjects = GameObject.FindGameObjectsWithTag("Map");
            if (mapObjects.Length > 0)
            {
                Bounds bounds = new Bounds(mapObjects[0].transform.position, Vector3.zero);
                foreach (var obj in mapObjects)
                {
                    bounds.Encapsulate(obj.transform.position);
                }
                return bounds;
            }
            
            // Fallback: estimate map size based on current map
            var currentMap = GetCurrentMap();
            switch (currentMap)
            {
                case "Skeld":
                    return new Bounds(Vector3.zero, new Vector3(40f, 40f, 0f));
                case "MIRA HQ":
                    return new Bounds(Vector3.zero, new Vector3(35f, 35f, 0f));
                case "Polus":
                    return new Bounds(Vector3.zero, new Vector3(50f, 50f, 0f));
                default:
                    return new Bounds(Vector3.zero, new Vector3(40f, 40f, 0f));
            }
        }

        private static string GetCurrentMap()
        {
            // Try to determine current map
            if (GameObject.Find("SkeldShip(Clone)") != null) return "Skeld";
            if (GameObject.Find("MiraShip(Clone)") != null) return "MIRA HQ";
            if (GameObject.Find("PolusShip(Clone)") != null) return "Polus";
            return "Unknown";
        }

        private static void ResetZoom()
        {
            currentZoom = 1f;
            if (configSmoothZoom.Value)
            {
                StartCoroutine(SmoothZoomCoroutine());
            }
            else
            {
                ApplyZoom();
            }
            
            // Reset camera position
            if (mainCamera != null)
            {
                mainCamera.transform.position = originalPosition;
            }
            
            CommonUtilities.SendChatMessage("Zoom reset to normal");
        }

        private static void FitToMap()
        {
            if (!configZoomToFit.Value) return;
            
            var mapBounds = GetMapBounds();
            if (mapBounds != null)
            {
                // Calculate zoom level to fit entire map
                float mapWidth = mapBounds.Value.size.x;
                float mapHeight = mapBounds.Value.size.y;
                float maxDimension = Mathf.Max(mapWidth, mapHeight);
                
                // Calculate required zoom level
                float requiredZoom = (maxDimension / 2f) / (mainCamera.orthographicSize / currentZoom);
                requiredZoom = Mathf.Clamp(requiredZoom, configMaxZoom.Value, configMinZoom.Value);
                
                SetZoom(requiredZoom);
                CenterCameraOnMap();
                
                CommonUtilities.SendChatMessage("Zoomed to fit entire map");
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for zoom commands
                if (chatText == "/zoom" || chatText == "/zoomhelp")
                {
                    ShowZoomHelp(__instance);
                    return false;
                }
                else if (chatText == "/zoomstatus")
                {
                    ShowZoomStatus(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/zoom "))
                {
                    string zoomValue = chatText.Substring(6);
                    SetZoomFromCommand(__instance, zoomValue);
                    return false;
                }
                else if (chatText == "/fitmap" || chatText == "/fit")
                {
                    FitToMap();
                    return false;
                }
                else if (chatText == "/resetzoom" || chatText == "/reset")
                {
                    ResetZoom();
                    return false;
                }
                
                return true;
            }
        }

        private static void ShowZoomHelp(PlayerControl player)
        {
            CommonUtilities.SendChatMessage("=== Map Zoom Commands ===");
            CommonUtilities.SendChatMessage($"Zoom In: {configZoomInKey.Value} or Mouse Wheel Up");
            CommonUtilities.SendChatMessage($"Zoom Out: {configZoomOutKey.Value} or Mouse Wheel Down");
            CommonUtilities.SendChatMessage($"Reset Zoom: {configResetZoomKey.Value}");
            CommonUtilities.SendChatMessage($"Fit to Map: {configFitToMapKey.Value}");
            CommonUtilities.SendChatMessage("/zoom <value> - Set specific zoom level");
            CommonUtilities.SendChatMessage("/fitmap - Fit entire map in view");
            CommonUtilities.SendChatMessage("/resetzoom - Reset to normal zoom");
        }

        private static void ShowZoomStatus(PlayerControl player)
        {
            float zoomPercentage = (1f / currentZoom) * 100f;
            CommonUtilities.SendChatMessage($"Current Zoom: {zoomPercentage:F1}%");
            CommonUtilities.SendChatMessage($"Zoom Level: {currentZoom:F2}");
            CommonUtilities.SendChatMessage($"Camera Size: {mainCamera?.orthographicSize:F2}");
        }

        private static void SetZoomFromCommand(PlayerControl player, string zoomValue)
        {
            if (float.TryParse(zoomValue, out float zoom))
            {
                if (zoom > 0f && zoom <= 10f)
                {
                    SetZoom(1f / zoom); // Convert percentage to zoom level
                    CommonUtilities.SendChatMessage($"Zoom set to {zoom}%");
                }
                else
                {
                    CommonUtilities.SendChatMessage("Zoom value must be between 0.1 and 10.0");
                }
            }
            else
            {
                CommonUtilities.SendChatMessage("Invalid zoom value. Use a number between 0.1 and 10.0");
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                // Reset zoom when new game starts
                currentZoom = 1f;
                if (mainCamera != null)
                {
                    mainCamera.orthographicSize = originalSize;
                    mainCamera.transform.position = originalPosition;
                }
            }
        }
    }
}
