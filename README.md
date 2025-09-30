# 🎮 Among Us Modding Collection

<div align="center">

![Among Us](https://img.shields.io/badge/Among%20Us-Modding%20Collection-red?style=for-the-badge&logo=gamepad)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.21-blue?style=for-the-badge)
![License](https://img.shields.io/badge/License-Educational-green?style=for-the-badge)

**A comprehensive collection of 17 BepInEx mods for Among Us that enhance gameplay, reduce trolling, and add exciting new features to improve the overall experience.**

[🚀 Quick Start](#-quick-start) • [📋 All Mods](#-all-mods) • [⚙️ Installation](#️-installation) • [🎯 Features](#-features)

</div>

---

## 🎯 Features

### 🛡️ **Anti-Trolling & Moderation**
- **Emergency Button Blocker** - Prevents first-round button spam
- **Vote Kick System** - Democratic player removal
- **Anti-Troll Tools** - Chat filtering, AFK detection, spam protection
- **Admin Panel** - Advanced host controls and permissions
- **Anti-Cheat System** - Comprehensive cheat detection and prevention

### 🎭 **Gameplay Enhancement**
- **Role Assignment System** - Custom roles with unique abilities
- **Task Progress Tracker** - Real-time completion monitoring
- **Meeting Timer** - Phase-based timing system
- **Death Animation Customizer** - Personalized death effects
- **Auto-Ready System** - Smart lobby management

### 📊 **Analytics & Recording**
- **Statistics Tracker** - Comprehensive player statistics
- **Replay System** - Game recording and playback
- **Player Behavior Analytics** - Advanced behavior tracking

### 🎨 **Customization & UI**
- **UI Customizer** - Themes, colors, and scaling
- **Custom Map Loader** - Map management and custom maps
- **Voice Chat Integration** - Proximity-based voice chat
- **Lobby Settings Presets** - Save/load configurations

## 📋 All Mods

| 🛡️ **Anti-Trolling & Moderation** | 🎭 **Gameplay Enhancement** | 📊 **Analytics & Recording** | 🎨 **Customization & UI** |
|:---:|:---:|:---:|:---:|
| 🚫 Emergency Button Blocker | 🎭 Role Assignment System | 📈 Statistics Tracker | 🎨 UI Customizer |
| 🗳️ Vote Kick System | 📊 Task Progress Tracker | 🎬 Replay System | 🗺️ Custom Map Loader |
| 🛡️ Anti-Troll Tools | ⏰ Meeting Timer | 📊 Player Behavior Analytics | 🎤 Voice Chat Integration |
| ⚙️ Admin Panel | 💀 Death Animation Customizer | | 💾 Lobby Settings Presets |
| 🛡️ Anti-Cheat System | 🚀 Auto-Ready System | | |

---

## 🚀 Quick Start

### 1️⃣ **Install BepInEx**
```bash
# Download BepInEx from GitHub
# Extract to your Among Us directory
# Run Among Us once to generate folders
```

### 2️⃣ **Install Mods**
```bash
# Compile mods into DLLs
# Place in BepInEx/plugins/ folder
# Launch Among Us
```

### 3️⃣ **Configure Settings**
```bash
# Each mod creates its own config file
# Located in BepInEx/config/
# Customize settings as needed
```

---

## ⚙️ Installation

1. **Install BepInEx** (if not already installed):
   - Download BepInEx from [GitHub](https://github.com/BepInEx/BepInEx/releases)
   - Extract to your Among Us game directory
   - Run Among Us once to generate the BepInEx folder structure

2. **Install the Mod**:
   - Compile this script into a DLL
   - Place the DLL in `BepInEx/plugins/` folder
   - Launch Among Us

## Project Structure

```
among-us-script/
├── mods/
│   ├── emergency-button-blocker/     # First round button protection
│   ├── vote-kick-system/            # Democratic player kicking
│   ├── role-assignment-system/      # Custom roles (Sheriff, Medic, etc.)
│   ├── anti-troll-tools/            # Chat filter, AFK detection, spam protection
│   ├── task-progress-tracker/       # Real-time task completion display
│   ├── meeting-timer/               # Discussion and voting phase timers
│   ├── death-animation-customizer/ # Custom death effects and sounds
│   ├── auto-ready-system/           # Smart auto-ready with lobby management
│   └── statistics-tracker/           # Player statistics and leaderboards
├── shared/
│   └── CommonUtilities.cs           # Shared utilities for all mods
└── README.md                        # This documentation
```

## How It Works

Each mod uses Harmony patches to intercept and modify game behavior:

- **Harmony Patches**: Runtime method interception and modification
- **BepInEx Framework**: Plugin loading and configuration management
- **Shared Utilities**: Common functions used across all mods
- **Configuration**: Individual settings for each mod

## Configuration

Each mod creates its own configuration file in `BepInEx/config/`:

- `com.yourname.emergencybuttonblocker.cfg` - Emergency Button Blocker settings
- `com.yourname.votekicksystem.cfg` - Vote Kick System settings  
- `com.yourname.roleassignmentsystem.cfg` - Role Assignment settings
- `com.yourname.antitrolltools.cfg` - Anti-Troll Tools settings
- `com.yourname.taskprogresstracker.cfg` - Task Progress settings
- `com.yourname.meetingtimer.cfg` - Meeting Timer settings
- `com.yourname.deathanimationcustomizer.cfg` - Death Animation settings
- `com.yourname.autoreadysystem.cfg` - Auto-Ready settings
- `com.yourname.statisticstracker.cfg` - Statistics settings

Example configuration:
```ini
[General]
## Enable/disable the mod
# Setting type: Boolean
# Default value: true
Enabled = true

[UI]
## Show visual elements
# Setting type: Boolean
# Default value: true
ShowUI = true
```

## Building from Source

1. Create a new C# Class Library project
2. Add references to:
   - `BepInEx.dll`
   - `0Harmony.dll`
   - `Assembly-CSharp.dll` (from Among Us)
3. Compile and place the resulting DLL in the plugins folder

## Compatibility

- **Among Us Version**: 2023.11.28 or later
- **BepInEx Version**: 5.4.21 or later
- **Platform**: Windows (Steam/Epic Games)

## 💬 Chat Commands

### 🛡️ **Moderation Commands**
| Command | Description | Mod |
|:---:|:---:|:---:|
| `/votekick <player>` | Vote to kick a player | Vote Kick System |
| `/mute <player>` | Mute/unmute a player | Anti-Troll Tools |
| `/admin <password>` | Authenticate as admin | Admin Panel |
| `/kick <player>` | Kick a player (admin) | Admin Panel |
| `/ban <player>` | Ban a player (admin) | Admin Panel |

### 🎮 **Gameplay Commands**
| Command | Description | Mod |
|:---:|:---:|:---:|
| `/tasks` or `/progress` | Check task progress | Task Progress Tracker |
| `/timer` or `/time` | Show meeting time | Meeting Timer |
| `/ready`, `/unready` | Manage ready states | Auto-Ready System |
| `/stats` or `/statistics` | View your statistics | Statistics Tracker |
| `/leaderboard` or `/lb` | Show leaderboard | Statistics Tracker |

### 🎨 **Customization Commands**
| Command | Description | Mod |
|:---:|:---:|:---:|
| `/color <name>` | Set HUD color | UI Customizer |
| `/theme <name>` | Change UI theme | UI Customizer |
| `/scale <value>` | Set UI scale | UI Customizer |
| `/loadpreset <name>` | Load lobby preset | Lobby Settings Presets |
| `/savepreset <name>` | Save current settings | Lobby Settings Presets |

### 📊 **Analytics Commands**
| Command | Description | Mod |
|:---:|:---:|:---:|
| `/analytics` | Show behavior analytics | Behavior Analytics |
| `/flagged` | Show flagged players | Behavior Analytics |
| `/replays` | List available replays | Replay System |
| `/playreplay <id>` | Play a replay | Replay System |
| `/anticheat` | Show anti-cheat status | Anti-Cheat System |

---

## 📁 Project Structure

```
among-us-script/
├── 📁 mods/
│   ├── 🚫 emergency-button-blocker/     # First round protection
│   ├── 🗳️ vote-kick-system/            # Democratic player kicking
│   ├── 🎭 role-assignment-system/      # Custom roles (Sheriff, Medic, etc.)
│   ├── 🛡️ anti-troll-tools/            # Chat filter, AFK detection, spam protection
│   ├── 📊 task-progress-tracker/       # Real-time task completion display
│   ├── ⏰ meeting-timer/               # Discussion and voting phase timers
│   ├── 💀 death-animation-customizer/ # Custom death effects and sounds
│   ├── 🚀 auto-ready-system/           # Smart auto-ready with lobby management
│   ├── 📈 statistics-tracker/           # Player statistics and leaderboards
│   ├── 🎬 replay-system/               # Game recording and playback
│   ├── 📊 behavior-analytics/          # Player behavior tracking and analysis
│   ├── 🗺️ custom-map-loader/          # Map management and custom maps
│   ├── 🎤 voice-chat-integration/      # Proximity-based voice chat
│   ├── 🎨 ui-customizer/               # Themes, colors, and UI scaling
│   ├── ⚙️ admin-panel/                # Advanced host controls and permissions
│   ├── 🛡️ anti-cheat-system/          # Comprehensive cheat detection
│   └── 💾 lobby-settings-presets/      # Save/load lobby configurations
├── 📁 shared/
│   └── 🔧 CommonUtilities.cs           # Shared utilities for all mods
└── 📄 README.md                        # This documentation
```

---

## ⚠️ Important Notes

- **Host-Only**: Most mods only affect the host's game
- **Universal Benefits**: All players in the lobby benefit from the mods
- **Auto-Reset**: Mods automatically reset when a new game starts
- **Logging**: All actions are logged for debugging purposes
- **Help Command**: Use `/help` in-game to see available commands

---

## 🔧 Troubleshooting

### ❌ **Common Issues**

| Issue | Solution |
|:---:|:---:|
| Mod not loading | Check BepInEx installation and DLL placement |
| Configuration errors | Verify config files in `BepInEx/config/` |
| Compatibility issues | Ensure Among Us and BepInEx are updated |
| Performance problems | Disable unnecessary mods or reduce settings |

### 🆘 **Getting Help**

1. **Check Console**: Look for error messages in BepInEx console
2. **Verify Installation**: Ensure all files are in correct locations
3. **Update Dependencies**: Keep BepInEx and Among Us updated
4. **Check Logs**: Review log files for specific error details

---

## 📄 License

<div align="center">

**This project is for educational purposes only.**

**Among Us is owned by InnerSloth LLC.**

---

### 🌟 **Star this repository if you found it helpful!**

[![GitHub stars](https://img.shields.io/github/stars/noahkhomer18/among-us-script?style=social)](https://github.com/noahkhomer18/among-us-script)
[![GitHub forks](https://img.shields.io/github/forks/noahkhomer18/among-us-script?style=social)](https://github.com/noahkhomer18/among-us-script)

</div>
