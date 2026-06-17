# Training Room – Weapons & Hand Gestures

## Overview
- **Training** is a dedicated room type (linked from Start). It focuses on perfecting one element at a time: gesture recognition and weapon skills, with clear VFX feedback.
- **AI/ML**: Gesture input goes through `IGestureRecognizer`. The default is `RuleBasedGestureRecognizer` (thresholds). You can replace it with an ML-based recognizer (e.g. ONNX or Unity ML Agents) that consumes the same `HandPoseData`.
- **VFX**: `TrainingVFX` on each station drives success (green), fail (red), and correction (yellow). Assign Particle Systems and optional Light in the Inspector for full effect.

## In Editor
1. **WorldManager → Dungeon Room Visualizer**: Assign **Mat Training** (e.g. a distinct material for the training pad).
2. **Training room**: Spawned automatically; `TrainingRoomManager` is added at runtime. With no **Training Content Prefab** assigned, default gesture + weapon stations are created.
3. **Testing without VR**: Use **HandPoseStub** – keys **1** = Pinch, **2** = Fist, **3** = Open Palm. **J** triggers weapon skill at the weapon station.
4. **VFX**: Add Particle Systems to the station GameObjects and assign them in **TrainingVFX** (Success / Fail / Correction). Add a **Light** for instant color feedback.

## Replacing with ML
- Implement `IGestureRecognizer` in a new component that:
  - Takes `HandPoseData` (from XR Hands or your pipeline).
  - Returns `GestureType` (e.g. from an ONNX model or trained classifier).
- Assign that component as the **Recognizer Component** on `GestureTrainingStation`.

## Adding XR Hands
- When using Meta/OpenXR Hands, fill `HandPoseData` from hand joints (palm position, pinch/grip from the XR Hand API) and pass it to your recognizer. Keep using the same `GestureType` enum and `TrainingVFX` flow.
