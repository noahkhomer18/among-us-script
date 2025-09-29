using System;
using System.Collections.Generic;
using UnityEngine;

namespace AmongUsMods.Shared
{
    /// <summary>
    /// Common utilities and helper methods for Among Us mods
    /// </summary>
    public static class CommonUtilities
    {
        /// <summary>
        /// Log a message with timestamp and mod name
        /// </summary>
        public static void LogMessage(string modName, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            Debug.Log($"[{timestamp}] [{modName}] {message}");
        }

        /// <summary>
        /// Check if a player is the host
        /// </summary>
        public static bool IsHost(PlayerControl player)
        {
            return player.AmOwner && AmongUsClient.Instance.AmHost;
        }

        /// <summary>
        /// Get all alive players
        /// </summary>
        public static List<PlayerControl> GetAlivePlayers()
        {
            var alivePlayers = new List<PlayerControl>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (!player.Data.IsDead && !player.Data.Disconnected)
                {
                    alivePlayers.Add(player);
                }
            }
            return alivePlayers;
        }

        /// <summary>
        /// Get all dead players
        /// </summary>
        public static List<PlayerControl> GetDeadPlayers()
        {
            var deadPlayers = new List<PlayerControl>();
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.Data.IsDead && !player.Data.Disconnected)
                {
                    deadPlayers.Add(player);
                }
            }
            return deadPlayers;
        }

        /// <summary>
        /// Check if the game is in a meeting
        /// </summary>
        public static bool IsInMeeting()
        {
            return MeetingHud.Instance != null;
        }

        /// <summary>
        /// Check if the game is in lobby
        /// </summary>
        public static bool IsInLobby()
        {
            return GameStartManager.Instance != null;
        }

        /// <summary>
        /// Get current game state
        /// </summary>
        public static GameState GetGameState()
        {
            if (IsInLobby()) return GameState.Lobby;
            if (IsInMeeting()) return GameState.Meeting;
            return GameState.InGame;
        }

        /// <summary>
        /// Send a chat message to all players
        /// </summary>
        public static void SendChatMessage(string message)
        {
            if (HudManager.Instance != null && HudManager.Instance.Chat != null)
            {
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, message);
            }
        }
    }

    public enum GameState
    {
        Lobby,
        InGame,
        Meeting
    }
}
