# Among Us Emergency Button Blocker

A BepInEx mod for Among Us that prevents players from calling the emergency button during the first round of the game, helping to reduce trolling and improve gameplay experience.

## Features

- **First Round Protection**: Blocks emergency button calls during the first meeting
- **Automatic Reset**: Resets for each new game
- **Configurable**: Can be enabled/disabled via configuration
- **Non-Intrusive**: Works seamlessly with existing gameplay

## Installation

1. **Install BepInEx** (if not already installed):
   - Download BepInEx from [GitHub](https://github.com/BepInEx/BepInEx/releases)
   - Extract to your Among Us game directory
   - Run Among Us once to generate the BepInEx folder structure

2. **Install the Mod**:
   - Compile this script into a DLL
   - Place the DLL in `BepInEx/plugins/` folder
   - Launch Among Us

## How It Works

The mod uses Harmony patches to intercept emergency button calls:

- **EmergencyButtonPatch**: Blocks `CmdReportDeadBody` calls during the first round
- **MeetingStartPatch**: Tracks when the first meeting occurs
- **GameStartPatch**: Resets the blocker for new games

## Configuration

The mod creates a configuration file at `BepInEx/config/com.yourname.emergencybuttonblocker.cfg`:

```ini
[General]
## Enable/disable the emergency button blocker
# Setting type: Boolean
# Default value: true
Enabled = true
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

## Notes

- This mod only affects the host's game
- All players in the lobby will have the emergency button blocked during the first round
- The mod automatically resets when a new game starts
- Emergency button calls are logged for debugging purposes

## Troubleshooting

If the mod isn't working:

1. Check that BepInEx is properly installed
2. Verify the DLL is in the correct plugins folder
3. Check the BepInEx console for error messages
4. Ensure Among Us is updated to a compatible version

## License

This project is for educational purposes. Among Us is owned by InnerSloth LLC.
