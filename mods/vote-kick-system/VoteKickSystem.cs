using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUsMods.Shared;

namespace AmongUsMods.VoteKickSystem
{
    [BepInPlugin("com.yourname.votekicksystem", "Vote Kick System", "1.0.0")]
    public class VoteKickSystemPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> configEnabled;
        private static ConfigEntry<int> configRequiredVotes;
        private static ConfigEntry<float> configVoteTimeLimit;
        
        private static Dictionary<byte, List<byte>> activeVotes = new Dictionary<byte, List<byte>>();
        private static Dictionary<byte, float> voteStartTimes = new Dictionary<byte, float>();

        private void Awake()
        {
            configEnabled = Config.Bind("General", "Enabled", true, "Enable/disable the vote kick system");
            configRequiredVotes = Config.Bind("General", "RequiredVotes", 3, "Number of votes required to kick a player");
            configVoteTimeLimit = Config.Bind("General", "VoteTimeLimit", 30f, "Time limit for vote kick in seconds");
            
            var harmony = new Harmony("com.yourname.votekicksystem");
            harmony.PatchAll();
            
            CommonUtilities.LogMessage("VoteKickSystem", "Vote Kick System loaded successfully!");
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
        public static class ChatPatch
        {
            public static bool Prefix(PlayerControl __instance, string chatText)
            {
                if (!configEnabled.Value) return true;

                // Check for vote kick commands
                if (chatText.StartsWith("/votekick ") || chatText.StartsWith("/vk "))
                {
                    string targetName = chatText.Split(' ')[1];
                    HandleVoteKick(__instance, targetName);
                    return false; // Block the chat message
                }

                return true;
            }
        }

        private static void HandleVoteKick(PlayerControl voter, string targetName)
        {
            var targetPlayer = FindPlayerByName(targetName);
            if (targetPlayer == null)
            {
                CommonUtilities.SendChatMessage($"Player '{targetName}' not found!");
                return;
            }

            if (targetPlayer.PlayerId == voter.PlayerId)
            {
                CommonUtilities.SendChatMessage("You cannot vote kick yourself!");
                return;
            }

            if (!activeVotes.ContainsKey(targetPlayer.PlayerId))
            {
                activeVotes[targetPlayer.PlayerId] = new List<byte>();
                voteStartTimes[targetPlayer.PlayerId] = Time.time;
                CommonUtilities.SendChatMessage($"Vote kick initiated against {targetPlayer.Data.PlayerName}");
            }

            if (!activeVotes[targetPlayer.PlayerId].Contains(voter.PlayerId))
            {
                activeVotes[targetPlayer.PlayerId].Add(voter.PlayerId);
                int currentVotes = activeVotes[targetPlayer.PlayerId].Count;
                int requiredVotes = configRequiredVotes.Value;
                
                CommonUtilities.SendChatMessage($"Vote kick: {currentVotes}/{requiredVotes} votes for {targetPlayer.Data.PlayerName}");
                
                if (currentVotes >= requiredVotes)
                {
                    KickPlayer(targetPlayer);
                }
            }
            else
            {
                CommonUtilities.SendChatMessage("You have already voted to kick this player!");
            }
        }

        private static PlayerControl FindPlayerByName(string name)
        {
            return PlayerControl.AllPlayerControls.FirstOrDefault(p => 
                p.Data.PlayerName.ToLower().Contains(name.ToLower()));
        }

        private static void KickPlayer(PlayerControl player)
        {
            CommonUtilities.SendChatMessage($"Player {player.Data.PlayerName} has been vote kicked!");
            activeVotes.Remove(player.PlayerId);
            voteStartTimes.Remove(player.PlayerId);
            
            // Kick the player
            if (AmongUsClient.Instance.AmHost)
            {
                AmongUsClient.Instance.KickPlayer(player.PlayerId, false);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
        public static class MeetingStartPatch
        {
            public static void Postfix()
            {
                // Clear all active votes when meeting starts
                activeVotes.Clear();
                voteStartTimes.Clear();
            }
        }

        private void Update()
        {
            if (!configEnabled.Value) return;

            // Check for expired votes
            var expiredVotes = new List<byte>();
            foreach (var vote in voteStartTimes)
            {
                if (Time.time - vote.Value > configVoteTimeLimit.Value)
                {
                    expiredVotes.Add(vote.Key);
                }
            }

            foreach (var expiredVote in expiredVotes)
            {
                activeVotes.Remove(expiredVote);
                voteStartTimes.Remove(expiredVote);
                CommonUtilities.SendChatMessage("Vote kick expired due to time limit");
            }
        }
    }
}
