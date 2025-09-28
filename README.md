# Aetheria VR RPG
Aetheria: The Living World - VR RPG with AI-driven procedural generation

## Project Overview
A Quest 3 (Android/OpenXR) Unity project featuring:
- Infinite seed-based world generation (chunked streaming)
- Player voice/text → STT → LLM intent → generator parameter pipeline
- Growing dungeons (dungeons expand as players interact)
- AI-generated opponents scaled by player rank, leveling, skills, and secret items
- Multiplayer (small teams), authoritative server for world/dungeon state

## Quick Start - Hello VR

### Prerequisites
- Unity 2022.3 LTS or later
- Meta Quest 3 device
- Android SDK and ADB installed
- OpenXR plugin for Unity

### Unity Project Setup

1. **Open Unity Project:**
   ```bash
   # Navigate to Unity project
   cd unity-project
   # Open Unity Hub and open this folder as a project
   ```

2. **Configure XR Settings:**
   - Window > XR Plug-in Management
   - Check "Initialize XR on Startup"
   - Add "OpenXR" provider
   - Configure OpenXR for Quest 3

3. **Configure XR Settings:**
   - Window > XR Plug-in Management
   - Check "Initialize XR on Startup"
   - Add "OpenXR" provider
   - Configure OpenXR for Quest 3

4. **Create HelloVR Scene:**
   - Follow instructions in `Assets/Scenes/HelloVR_SetupInstructions.md`
   - This creates a basic VR scene with teleportation and interactable objects

5. **Test in Editor:**
   - Press Play in Unity
   - Use mouse to look around (simulates VR headset)
   - Check Console for debug messages

### Building for Quest 3

1. **Configure Build Settings:**
   - File > Build Settings
   - Switch Platform to Android
   - Add Open Scenes: HelloVR

2. **Player Settings:**
   - Company Name: Your Company
   - Product Name: Aetheria VR
   - Package Name: com.yourcompany.aetheria
   - Minimum API Level: 23 (Android 6.0)
   - Target API Level: 33 (Android 13)

3. **Build and Deploy:**
   ```bash
   # Build APK
   # In Unity: Build Settings > Build
   # Save as: aetheria-hello-vr.apk
   
   # Install on Quest 3
   adb install aetheria-hello-vr.apk
   
   # Launch app
   adb shell am start -n com.yourcompany.aetheria/.UnityPlayerActivity
   ```

### Testing on Quest 3

1. **Put on Quest 3 headset**
2. **Launch Aetheria VR from Unknown Sources**
3. **Test VR Controls:**
   - Look around with head movement
   - Press trigger on either controller to teleport forward
   - Grab the red cube with controller grip buttons
   - Move around the scene

### Expected Behavior
- Scene loads without errors
- Player can teleport forward by pressing controller triggers
- Interactable cube can be grabbed and moved
- Debug logs appear in Unity Console (when connected via ADB)

## Development Workflow

### Branch Structure
- `feature/hello-vr` - Basic VR setup and Hello World scene
- `feature/chunk-gen` - Procedural chunk generation system
- `feature/voice-pipeline` - Voice-to-text integration
- `feature/llm-integration` - LLM parameter mapping
- `feature/dungeon-growth` - Dynamic dungeon expansion
- `feature/multiplayer` - Multiplayer networking

### Testing
- Unity Test Runner for C# unit tests
- PlayMode tests for VR functionality
- Manual testing on Quest 3 device

## Next Steps

After completing Hello VR setup:
1. **Chunked World Generation** - Implement deterministic chunk generator
2. **Voice Pipeline** - Add STT integration for voice commands
3. **LLM Integration** - Connect voice/text to world generation parameters
4. **Dungeon System** - Create growing, interactive dungeons
5. **Multiplayer** - Add networking for team-based gameplay

## Troubleshooting

### Common Issues
- **XR not initializing:** Check OpenXR plugin installation and Quest 3 developer mode
- **Build fails:** Verify Android SDK and build tools are installed
- **App crashes on Quest:** Check Unity Console via ADB for error logs
- **Controllers not working:** Ensure XR Interaction Manager is in scene

### Debug Commands
```bash
# View Unity logs on Quest
adb logcat -s Unity

# Check connected devices
adb devices

# Uninstall app
adb uninstall com.yourcompany.aetheria
```
