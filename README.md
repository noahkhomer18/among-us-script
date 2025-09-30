# Among Us Modding Collection

A comprehensive collection of BepInEx mods for Among Us that enhance gameplay, reduce trolling, and add new features to improve the overall experience.

## Available Mods

### 🚫 Emergency Button Blocker
- **First Round Protection**: Blocks emergency button calls during the first meeting
- **Automatic Reset**: Resets for each new game
- **Configurable**: Can be enabled/disabled via configuration

### 🗳️ Vote Kick System
- **Democratic Kicking**: Players can vote to kick toxic players
- **Configurable Thresholds**: Set required votes and time limits
- **Chat Commands**: Use `/votekick` or `/vk` commands

### 🎭 Role Assignment System
- **Custom Roles**: Sheriff, Medic, Engineer, Jester
- **Balanced Gameplay**: Each role has unique abilities
- **Configurable**: Enable/disable specific roles

### 🛡️ Anti-Troll Tools
- **Chat Filter**: Blocks inappropriate language and spam
- **AFK Detection**: Automatically kicks inactive players
- **Spam Protection**: Limits messages per minute per player

### 📊 Task Progress Tracker
- **Real-time Progress**: Shows task completion percentages
- **Visual Display**: Progress bar and percentage indicators
- **Chat Commands**: Use `/tasks` or `/progress` to check status

### ⏰ Meeting Timer
- **Phase Timers**: Separate timers for discussion and voting
- **Visual Countdown**: On-screen timer display
- **Auto-Transition**: Automatically moves between phases

### 💀 Death Animation Customizer
- **Custom Effects**: Personalized death animations and effects
- **Sound Effects**: Custom death sounds and audio
- **Particle Systems**: Enhanced visual death effects

### 🚀 Auto-Ready System
- **Smart Ready**: Automatically ready up when lobby is full
- **Lobby Management**: Advanced ready state tracking
- **Chat Commands**: Use `/ready`, `/unready`, `/readycount`

### 📈 Statistics Tracker
- **Comprehensive Stats**: Track wins, kills, tasks, play time
- **Leaderboards**: Compare performance with other players
- **Data Persistence**: Save statistics between games

## Installation

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

## Chat Commands

Many mods include chat commands for enhanced functionality:

- `/votekick <player>` or `/vk <player>` - Vote to kick a player
- `/tasks` or `/progress` - Check your task progress
- `/timer` or `/time` - Show meeting time remaining
- `/ready`, `/unready`, `/readycount` - Manage ready states
- `/stats` or `/statistics` - View your statistics
- `/leaderboard` or `/lb` - Show player leaderboard

## Notes

- Most mods only affect the host's game
- All players in the lobby will benefit from the mods
- Mods automatically reset when a new game starts
- All actions are logged for debugging purposes
- Use `/help` in-game to see available commands

## Troubleshooting

If the mod isn't working:

1. Check that BepInEx is properly installed
2. Verify the DLL is in the correct plugins folder
3. Check the BepInEx console for error messages
4. Ensure Among Us is updated to a compatible version

## License

This project is for educational purposes. Among Us is owned by InnerSloth LLC.
