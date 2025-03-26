SnapTap | Make every keystroke count.

**SnapTap** is a lightweight, high-performance keyboard enhancement utility designed specifically for gamers. 
It provides instant directional switching and eliminates the "sticky key" problem experienced in many games, giving you a competitive edge with more responsive movement controls.

 ✨ Features

- **Instant Direction Switching**: Eliminates the delay when changing directions using WASD keys
- **Smart Key Management**: Intelligently handles key presses to prevent conflicting inputs
- **Zero Input Lag**: Uses low-level keyboard hooks for minimal latency
- **Game-Specific Detection**: Automatically activates only in supported game windows
- **Chat Mode Detection**: Automatically disables when you're typing in game chat
- **Minimal Resource Usage**: Runs silently in your system tray with negligible CPU/memory footprint
- **No Macros, No Cheating**: Enhances your natural gameplay without breaking game rules

## 🎮 Why SnapTap?

Have you ever pressed both 'A' and 'D' keys simultaneously in a game, only to get stuck moving in one direction? Or experienced that frustrating delay when switching from forward to backward? SnapTap solves these problems.

Traditional keyboards and games process key events sequentially, leading to input conflicts and delays. 
SnapTap intelligently manages these key presses using key-grouping technology to ensure only one directional input is active at a time, providing instant, responsive control.

## 🔧 How It Works

SnapTap uses Windows low-level keyboard hooks to intercept and manage WASD key presses in real-time. Keys are organized into logical groups (`A`/`D` for horizontal movement, `W`/`S` for vertical movement). When you press a new key in a group while another is active, SnapTap instantly releases the previous key and activates the new one, eliminating the delay and conflict that occurs in normal keyboard processing.

## 📥 Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/SnapTap/releases) page
2. Extract the ZIP file to a location of your choice
3. Run `SnapTap.exe` to start the application
4. The application will appear in your system tray

**Optional**: Add SnapTap to your startup programs to have it automatically run when Windows starts.

## 🚀 Usage

1. Launch SnapTap before starting your game
2. The application runs silently in your system tray (look for the SnapTap icon)
3. SnapTap automatically detects supported game windows and activates only when they're in focus
4. Right-click the tray icon for options:
   - Enable/Disable SnapTap
   - Exit the application

### Supported Games

SnapTap works with most games using standard input methods, including games developed with:
- Unity Engine (UnityWndClass)
- Source Engine (Valve001)
- Unreal Engine (UnrealWindow)
- SDL-based games (SDL_app)

## ⚙️ Configuration

Currently, SnapTap uses a default configuration optimized for WASD movement keys. Future releases will include a configuration utility to customize key groups and behaviors.

## 💻 System Requirements

- Windows 10 or Windows 11
- .NET Framework 4.5 or higher
- Minimal CPU and memory usage
- No special hardware required

## ❓ Troubleshooting

### SnapTap doesn't seem to be working in my game

- Ensure SnapTap is running (check system tray)
- Verify the "Enabled" option is checked in the tray menu
- Make sure your game is using one of the supported window classes
- Some games with custom anti-cheat systems may block keyboard hooks

### Keys get stuck or behave unexpectedly

- Right-click the SnapTap icon and toggle the "Enabled" option off and on
- Restart the application or your game
- Make sure no other keyboard enhancement software is running simultaneously

## 🔜 Planned Features

- Customizable key groups
- Profiles for different games
- GUI for configuration
- Extended game compatibility
- Additional movement enhancements
  
## 🙏 Acknowledgments

- Inspired by various keyboard enhancement utilities but built from scratch for optimal performance


SnapTap | Make every keystroke count.

*Note: SnapTap does not modify game files or interfere with game integrity. It only manages keyboard input at the system level.*
