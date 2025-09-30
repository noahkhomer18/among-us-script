using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.AdminPanel
{
    [BepInPlugin("com.yourname.adminpanel", "Admin Panel", "1.0.0")]
    public class AdminPanelPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configShowAdminPanel;
        private static ConfigEntry<bool> configAllowKick;
        private static ConfigEntry<bool> configAllowBan;
        private static ConfigEntry<bool> configAllowTeleport;
        private static ConfigEntry<bool> configAllowSpectate;
        private static ConfigEntry<bool> configAllowGodMode;
        private static ConfigEntry<string> configAdminPassword;
        
        private static Dictionary<byte, AdminPermissions> adminPermissions = new Dictionary<byte, AdminPermissions>();
        private static List<byte> bannedPlayers = new List<byte>();
        private static GameObject adminPanelUI;
        private static bool isAdminPanelOpen = false;

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable admin panel");
            configShowAdminPanel = Config.Bind("UI", "ShowAdminPanel", true, "Show admin panel UI");
            configAllowKick = Config.Bind("Permissions", "AllowKick", true, "Allow kicking players");
            configAllowBan = Config.Bind("Permissions", "AllowBan", true, "Allow banning players");
            configAllowTeleport = Config.Bind("Permissions", "AllowTeleport", true, "Allow teleporting players");
            configAllowSpectate = Config.Bind("Permissions", "AllowSpectate", true, "Allow spectating players");
            configAllowGodMode = Config.Bind("Permissions", "AllowGodMode", true, "Allow god mode");
            configAdminPassword = Config.Bind("Security", "AdminPassword", "admin123", "Admin panel password");
            
            var harmony = new Harmony("com.yourname.adminpanel");
            harmony.PatchAll();
            
            InitializeAdminPanel();
            CommonUtilities.LogMessage("AdminPanel", "Admin Panel loaded successfully!");
        }

        private static void InitializeAdminPanel()
        {
            // Initialize admin permissions for host
            if (AmongUsClient.Instance.AmHost)
            {
                var hostPermissions = new AdminPermissions
                {
                    CanKick = configAllowKick.Value,
                    CanBan = configAllowBan.Value,
                    CanTeleport = configAllowTeleport.Value,
                    CanSpectate = configAllowSpectate.Value,
                    CanGodMode = configAllowGodMode.Value,
                    IsAdmin = true
                };
                
                adminPermissions[PlayerControl.LocalPlayer.PlayerId] = hostPermissions;
            }
            
            if (configShowAdminPanel.Value)
            {
                CreateAdminPanelUI();
            }
        }

        private static void CreateAdminPanelUI()
        {
            adminPanelUI = new GameObject("AdminPanelUI");
            adminPanelUI.transform.SetParent(HudManager.Instance.transform);
            adminPanelUI.SetActive(false);
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class AdminCommandPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;
                
                // Check for admin commands
                if (chatText.StartsWith("/admin "))
                {
                    string password = chatText.Substring(7);
                    AuthenticateAdmin(__instance, password);
                    return false;
                }
                else if (chatText == "/adminpanel" || chatText == "/ap")
                {
                    ToggleAdminPanel(__instance);
                    return false;
                }
                else if (chatText.StartsWith("/kick "))
                {
                    string targetName = chatText.Substring(6);
                    KickPlayer(__instance, targetName);
                    return false;
                }
                else if (chatText.StartsWith("/ban "))
                {
                    string targetName = chatText.Substring(5);
                    BanPlayer(__instance, targetName);
                    return false;
                }
                else if (chatText.StartsWith("/teleport "))
                {
                    string targetName = chatText.Substring(10);
                    TeleportPlayer(__instance, targetName);
                    return false;
                }
                else if (chatText.StartsWith("/spectate "))
                {
                    string targetName = chatText.Substring(10);
                    SpectatePlayer(__instance, targetName);
                    return false;
                }
                else if (chatText == "/godmode" || chatText == "/gm")
                {
                    ToggleGodMode(__instance);
                    return false;
                }
                else if (chatText == "/adminhelp")
                {
                    ShowAdminHelp(__instance);
                    return false;
                }
                
                return true;
            }
        }

        private static void AuthenticateAdmin(PlayerControl player, string password)
        {
            if (password == configAdminPassword.Value)
            {
                var permissions = new AdminPermissions
                {
                    CanKick = configAllowKick.Value,
                    CanBan = configAllowBan.Value,
                    CanTeleport = configAllowTeleport.Value,
                    CanSpectate = configAllowSpectate.Value,
                    CanGodMode = configAllowGodMode.Value,
                    IsAdmin = true
                };
                
                adminPermissions[player.PlayerId] = permissions;
                CommonUtilities.SendChatMessage($"{player.Data.PlayerName} is now an admin!");
            }
            else
            {
                CommonUtilities.SendChatMessage("Invalid admin password!");
            }
        }

        private static void ToggleAdminPanel(PlayerControl player)
        {
            if (!IsPlayerAdmin(player))
            {
                CommonUtilities.SendChatMessage("You don't have admin permissions!");
                return;
            }
            
            isAdminPanelOpen = !isAdminPanelOpen;
            if (adminPanelUI != null)
            {
                adminPanelUI.SetActive(isAdminPanelOpen);
            }
            
            CommonUtilities.SendChatMessage($"Admin panel {(isAdminPanelOpen ? "opened" : "closed")}");
        }

        private static void KickPlayer(PlayerControl admin, string targetName)
        {
            if (!IsPlayerAdmin(admin) || !adminPermissions[admin.PlayerId].CanKick)
            {
                CommonUtilities.SendChatMessage("You don't have permission to kick players!");
                return;
            }
            
            var targetPlayer = FindPlayerByName(targetName);
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found!");
                return;
            }
            
            if (targetPlayer.PlayerId == admin.PlayerId)
            {
                CommonUtilities.SendChatMessage("You cannot kick yourself!");
                return;
            }
            
            CommonUtilities.SendChatMessage($"{admin.Data.PlayerName} kicked {targetPlayer.Data.PlayerName}!");
            
            if (AmongUsClient.Instance.AmHost)
            {
                AmongUsClient.Instance.KickPlayer(targetPlayer.PlayerId, false);
            }
        }

        private static void BanPlayer(PlayerControl admin, string targetName)
        {
            if (!IsPlayerAdmin(admin) || !adminPermissions[admin.PlayerId].CanBan)
            {
                CommonUtilities.SendChatMessage("You don't have permission to ban players!");
                return;
            }
            
            var targetPlayer = FindPlayerByName(targetName);
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found!");
                return;
            }
            
            if (targetPlayer.PlayerId == admin.PlayerId)
            {
                CommonUtilities.SendChatMessage("You cannot ban yourself!");
                return;
            }
            
            bannedPlayers.Add(targetPlayer.PlayerId);
            CommonUtilities.SendChatMessage($"{admin.Data.PlayerName} banned {targetPlayer.Data.PlayerName}!");
            
            if (AmongUsClient.Instance.AmHost)
            {
                AmongUsClient.Instance.KickPlayer(targetPlayer.PlayerId, true);
            }
        }

        private static void TeleportPlayer(PlayerControl admin, string targetName)
        {
            if (!IsPlayerAdmin(admin) || !adminPermissions[admin.PlayerId].CanTeleport)
            {
                CommonUtilities.SendChatMessage("You don't have permission to teleport players!");
                return;
            }
            
            var targetPlayer = FindPlayerByName(targetName);
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found!");
                return;
            }
            
            // Teleport admin to target player
            admin.transform.position = targetPlayer.transform.position;
            CommonUtilities.SendChatMessage($"{admin.Data.PlayerName} teleported to {targetPlayer.Data.PlayerName}!");
        }

        private static void SpectatePlayer(PlayerControl admin, string targetName)
        {
            if (!IsPlayerAdmin(admin) || !adminPermissions[admin.PlayerId].CanSpectate)
            {
                CommonUtilities.SendChatMessage("You don't have permission to spectate players!");
                return;
            }
            
            var targetPlayer = FindPlayerByName(targetName);
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found!");
                return;
            }
            
            // Enable spectate mode
            admin.transform.position = targetPlayer.transform.position;
            admin.GetComponent<Collider2D>().enabled = false;
            admin.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.5f);
            
            CommonUtilities.SendChatMessage($"{admin.Data.PlayerName} is now spectating {targetPlayer.Data.PlayerName}!");
        }

        private static void ToggleGodMode(PlayerControl admin)
        {
            if (!IsPlayerAdmin(admin) || !adminPermissions[admin.PlayerId].CanGodMode)
            {
                CommonUtilities.SendChatMessage("You don't have permission to use god mode!");
                return;
            }
            
            // Toggle god mode (invincibility)
            var collider = admin.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = !collider.enabled;
            }
            
            string status = collider.enabled ? "disabled" : "enabled";
            CommonUtilities.SendChatMessage($"God mode {status} for {admin.Data.PlayerName}!");
        }

        private static void ShowAdminHelp(PlayerControl player)
        {
            if (!IsPlayerAdmin(player))
            {
                CommonUtilities.SendChatMessage("You don't have admin permissions!");
                return;
            }
            
            CommonUtilities.SendChatMessage("=== Admin Commands ===");
            CommonUtilities.SendChatMessage("/admin <password> - Authenticate as admin");
            CommonUtilities.SendChatMessage("/adminpanel - Toggle admin panel");
            CommonUtilities.SendChatMessage("/kick <player> - Kick a player");
            CommonUtilities.SendChatMessage("/ban <player> - Ban a player");
            CommonUtilities.SendChatMessage("/teleport <player> - Teleport to player");
            CommonUtilities.SendChatMessage("/spectate <player> - Spectate a player");
            CommonUtilities.SendChatMessage("/godmode - Toggle god mode");
        }

        private static bool IsPlayerAdmin(PlayerControl player)
        {
            return adminPermissions.ContainsKey(player.PlayerId) && 
                   adminPermissions[player.PlayerId].IsAdmin;
        }

        private static PlayerControl FindPlayerByName(string name)
        {
            return PlayerControl.AllPlayerControls.FirstOrDefault(p => 
                p.Data.PlayerName.ToLower().Contains(name.ToLower()));
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnDestroy))]
        public static class PlayerDestroyPatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                // Clean up admin permissions
                if (adminPermissions.ContainsKey(__instance.PlayerId))
                {
                    adminPermissions.Remove(__instance.PlayerId);
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                // Reset admin panel state
                isAdminPanelOpen = false;
                if (adminPanelUI != null)
                {
                    adminPanelUI.SetActive(false);
                }
            }
        }
    }

    [System.Serializable]
    public class AdminPermissions
    {
        public bool CanKick;
        public bool CanBan;
        public bool CanTeleport;
        public bool CanSpectate;
        public bool CanGodMode;
        public bool IsAdmin;
    }
}
