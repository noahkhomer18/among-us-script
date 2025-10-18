# 🚀 Installation Guide

## Prerequisites

- **Among Us** (Steam/Epic Games version)
- **BepInEx 5.4.21** or later
- **Windows 10/11** (64-bit)

## Step-by-Step Installation

### 1️⃣ Install BepInEx

1. Download BepInEx from [GitHub Releases](https://github.com/BepInEx/BepInEx/releases)
2. Extract the contents to your Among Us game directory
3. Run Among Us once to generate the BepInEx folder structure

### 2️⃣ Compile Mods

1. Open each mod in Visual Studio or your preferred C# IDE
2. Add references to:
   - `BepInEx.dll`
   - `0Harmony.dll`
   - `Assembly-CSharp.dll` (from Among Us)
3. Compile each mod into a DLL
4. Place the DLLs in `BepInEx/plugins/` folder

### 3️⃣ Configure Settings

1. Launch Among Us
2. Each mod will create its own config file in `BepInEx/config/`
3. Customize settings as needed

## Quick Setup Script

```bash
# Create plugins directory
mkdir "BepInEx/plugins"

# Copy compiled DLLs
copy "*.dll" "BepInEx/plugins/"

# Launch Among Us
start "Among Us.exe"
```

## Troubleshooting

### Common Issues

| Issue | Solution |
|:---:|:---:|
| Mod not loading | Check BepInEx installation and DLL placement |
| Configuration errors | Verify config files in `BepInEx/config/` |
| Compatibility issues | Ensure Among Us and BepInEx are updated |
| Performance problems | Disable unnecessary mods or reduce settings |

### Getting Help

1. **Check Console**: Look for error messages in BepInEx console
2. **Verify Installation**: Ensure all files are in correct locations
3. **Update Dependencies**: Keep BepInEx and Among Us updated
4. **Check Logs**: Review log files for specific error details

## Mod Compatibility

- **Among Us Version**: 2023.11.28 or later
- **BepInEx Version**: 5.4.21 or later
- **Platform**: Windows (Steam/Epic Games)

## Support

For issues and questions:
- Check the [README.md](README.md) for detailed documentation
- Review the [Troubleshooting](#troubleshooting) section
- Ensure all prerequisites are met
