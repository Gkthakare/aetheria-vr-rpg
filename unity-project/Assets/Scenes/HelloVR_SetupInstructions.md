# HelloVR Scene Setup Instructions

Since Unity scene files are binary, follow these steps to create the HelloVR scene:

## Scene Setup Steps

1. **Open Unity 2022.3 LTS or later**
2. **Create New Scene:**
   - File > New Scene
   - Choose "3D" template
   - Save as "HelloVR" in Assets/Scenes/

3. **Set up XR Origin:**
   - Delete the default Main Camera
   - Right-click in Hierarchy > XR > XR Origin (VR)
   - This creates: XR Origin, Main Camera, LeftHand Controller, RightHand Controller

4. **Configure XR Origin:**
   - Select XR Origin GameObject
   - Add Component: VRPlayerController script
   - In VRPlayerController component:
     - Drag XR Origin to "Xr Origin" field
     - Drag Main Camera to "Xr Camera" field
     - Set Teleport Distance: 5
     - Set Ground Layer Mask: Default (0)

5. **Add Ground Plane:**
   - Right-click in Hierarchy > 3D Object > Plane
   - Rename to "Ground"
   - Scale: (10, 1, 10) to make it larger
   - Position: (0, 0, 0)

6. **Add Interactable Cube:**
   - Right-click in Hierarchy > 3D Object > Cube
   - Rename to "InteractableCube"
   - Position: (0, 1, 3)
   - Scale: (1, 1, 1)
   - Add Component: Rigidbody
   - Add Component: XR Grab Interactable

7. **Add XR Interaction Manager:**
   - Right-click in Hierarchy > XR > Interaction Manager
   - This enables XR interactions

8. **Add Lighting:**
   - Right-click in Hierarchy > Light > Directional Light
   - Position: (0, 10, 0)
   - Rotation: (50, -30, 0)

## Expected Scene Hierarchy:
```
HelloVR
├── XR Origin
│   ├── Main Camera
│   ├── LeftHand Controller
│   └── RightHand Controller
├── Ground
├── InteractableCube
├── Interaction Manager
└── Directional Light
```

## Testing in Editor:
1. Press Play
2. Use mouse to look around (simulates VR headset)
3. Press Left Shift + Mouse to simulate controller input
4. The VRPlayerController should log debug messages

## XR Configuration (Important!):
1. Go to Window > XR Plug-in Management
2. Check "Initialize XR on Startup"
3. Add "OpenXR" provider
4. Configure OpenXR for Quest 3

## Build Settings:
1. File > Build Settings
2. Switch Platform to Android
3. Add Open Scenes: HelloVR
4. Player Settings:
   - Company Name: Your Company
   - Product Name: Aetheria VR
   - Package Name: com.yourcompany.aetheria
   - Minimum API Level: 23 (Android 6.0)
   - Target API Level: 33 (Android 13)
