using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.RoleAssignmentSystem
{
    [BepInPlugin("com.yourname.roleassignmentsystem", "Role Assignment System", "1.0.0")]
    public class RoleAssignmentSystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<bool> configSheriffEnabled;
        private static ConfigEntry<bool> configMedicEnabled;
        private static ConfigEntry<bool> configEngineerEnabled;
        private static ConfigEntry<bool> configJesterEnabled;
        
        private static Dictionary<byte, CustomRole> playerRoles = new Dictionary<byte, CustomRole>();
        private static List<byte> sheriffPlayers = new List<byte>();
        private static List<byte> medicPlayers = new List<byte>();
        private static List<byte> engineerPlayers = new List<byte>();
        private static List<byte> jesterPlayers = new List<byte>();

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the role assignment system");
            configSheriffEnabled = Config.Bind("Roles", "SheriffEnabled", true, "Enable Sheriff role");
            configMedicEnabled = Config.Bind("Roles", "MedicEnabled", true, "Enable Medic role");
            configEngineerEnabled = Config.Bind("Roles", "EngineerEnabled", true, "Enable Engineer role");
            configJesterEnabled = Config.Bind("Roles", "JesterEnabled", true, "Enable Jester role");
            
            var harmony = new Harmony("com.yourname.roleassignmentsystem");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("RoleAssignmentSystem", "Role Assignment System loaded successfully!");
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        public static class GameStartPatch
        {
            public static void Postfix()
            {
                if (!configEnabled.Value) return;
                
                AssignRoles();
            }
        }

        private static void AssignRoles()
        {
            playerRoles.Clear();
            sheriffPlayers.Clear();
            medicPlayers.Clear();
            engineerPlayers.Clear();
            jesterPlayers.Clear();

            var alivePlayers = CommonUtilities.GetAlivePlayers();
            if (alivePlayers.Count < 4) return; // Need at least 4 players for roles

            var shuffledPlayers = alivePlayers.OrderBy(x => UnityEngine.Random.value).ToList();
            int assignedCount = 0;

            // Assign Sheriff (1 player)
            if (configSheriffEnabled.Value && assignedCount < shuffledPlayers.Count)
            {
                var sheriff = shuffledPlayers[assignedCount];
                playerRoles[sheriff.PlayerId] = CustomRole.Sheriff;
                sheriffPlayers.Add(sheriff.PlayerId);
                assignedCount++;
                CommonUtilities.LogMessage("RoleAssignmentSystem", $"Assigned Sheriff to {sheriff.Data.PlayerName}");
            }

            // Assign Medic (1 player)
            if (configMedicEnabled.Value && assignedCount < shuffledPlayers.Count)
            {
                var medic = shuffledPlayers[assignedCount];
                playerRoles[medic.PlayerId] = CustomRole.Medic;
                medicPlayers.Add(medic.PlayerId);
                assignedCount++;
                CommonUtilities.LogMessage("RoleAssignmentSystem", $"Assigned Medic to {medic.Data.PlayerName}");
            }

            // Assign Engineer (1 player)
            if (configEngineerEnabled.Value && assignedCount < shuffledPlayers.Count)
            {
                var engineer = shuffledPlayers[assignedCount];
                playerRoles[engineer.PlayerId] = CustomRole.Engineer;
                engineerPlayers.Add(engineer.PlayerId);
                assignedCount++;
                CommonUtilities.LogMessage("RoleAssignmentSystem", $"Assigned Engineer to {engineer.Data.PlayerName}");
            }

            // Assign Jester (1 player)
            if (configJesterEnabled.Value && assignedCount < shuffledPlayers.Count)
            {
                var jester = shuffledPlayers[assignedCount];
                playerRoles[jester.PlayerId] = CustomRole.Jester;
                jesterPlayers.Add(jester.PlayerId);
                assignedCount++;
                CommonUtilities.LogMessage("RoleAssignmentSystem", $"Assigned Jester to {jester.Data.PlayerName}");
            }

            // Notify players of their roles
            foreach (var player in alivePlayers)
            {
                if (playerRoles.ContainsKey(player.PlayerId))
                {
                    var role = playerRoles[player.PlayerId];
                    CommonUtilities.SendChatMessage($"You are the {role}!");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class MurderPatch
        {
            public static bool Prefix(PlayerControl __instance, PlayerControl target)
            {
                if (!configEnabled.Value) return true;

                // Sheriff can only kill impostors
                if (playerRoles.ContainsKey(__instance.PlayerId) && 
                    playerRoles[__instance.PlayerId] == CustomRole.Sheriff)
                {
                    if (!target.Data.Role.IsImpostor)
                    {
                        // Sheriff killed innocent - sheriff dies instead
                        __instance.MurderPlayer(__instance);
                        CommonUtilities.SendChatMessage($"{__instance.Data.PlayerName} (Sheriff) killed an innocent and died!");
                        return false;
                    }
                }

                // Jester wins if killed
                if (playerRoles.ContainsKey(target.PlayerId) && 
                    playerRoles[target.PlayerId] == CustomRole.Jester)
                {
                    CommonUtilities.SendChatMessage($"{target.Data.PlayerName} (Jester) wins by being killed!");
                    // End game logic would go here
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Revive))]
        public static class RevivePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value) return;

                // Medic can revive players
                if (playerRoles.ContainsKey(__instance.PlayerId) && 
                    playerRoles[__instance.PlayerId] == CustomRole.Medic)
                {
                    // Medic revival logic would go here
                    CommonUtilities.SendChatMessage($"{__instance.Data.PlayerName} (Medic) has revival abilities!");
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class FixedUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!configEnabled.Value) return;

                // Engineer can use vents
                if (playerRoles.ContainsKey(__instance.PlayerId) && 
                    playerRoles[__instance.PlayerId] == CustomRole.Engineer)
                {
                    // Engineer vent access logic would go here
                }
            }
        }

        public static CustomRole GetPlayerRole(byte playerId)
        {
            return playerRoles.ContainsKey(playerId) ? playerRoles[playerId] : CustomRole.Crewmate;
        }

        public static bool HasRole(byte playerId, CustomRole role)
        {
            return playerRoles.ContainsKey(playerId) && playerRoles[playerId] == role;
        }
    }

    public enum CustomRole
    {
        Crewmate,
        Sheriff,
        Medic,
        Engineer,
        Jester
    }
}
