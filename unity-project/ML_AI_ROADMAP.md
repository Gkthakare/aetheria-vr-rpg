# Aetheria VR – ML + AI Roadmap

Run ML/AI work in parallel with gameplay. Copy-paste friendly for OneNote.

---

## Phase 1: VFX Image Models

### Goal
Train image models to generate or enhance VFX textures (fire, water, smoke, magic circles).

### Pipeline
```
Collect/curate VFX images → Train model (Python) → Export → Use in Unity
```

### 1.1 Data
- **Sources**: Your existing AI-generated textures (stone, water, lava, magic_circle, portal_surface)
- **Add**: Sprite sheets for particles (8-frame fire, 4-frame smoke)
- **Format**: 256×256 or 512×512 PNG, organized by type (fire/, water/, smoke/, magic/)

### 1.2 Training (Python)
- **Option A – Fine-tune Stable Diffusion**: Use LoRA on VFX-style images
- **Option B – Train small GAN**: Pix2Pix or StyleGAN for texture generation
- **Option C – Image-to-image**: Input noise/sketch → output VFX texture

### 1.3 Unity Integration
- **Pre-generated**: Export textures from Python → drop into `Assets/Art/Textures/ML_Generated/`
- **Runtime API**: Python server generates on demand → Unity fetches via HTTP (AIClient pattern)

### 1.4 Scripts to Add
- `ml-training/vfx_train.py` – training script outline
- `ml-training/export_textures.py` – batch export for Unity
- `Assets/Scripts/AI/VFXTextureRequest.cs` – optional runtime texture fetch

---

## Phase 2: Gesture Recognition (ML)

### Goal
Replace rule-based gesture recognizer with ML model for pinch, fist, open palm, thrust, shield.

### Pipeline
```
Collect hand pose data → Label → Train classifier → Export ONNX → Load in Unity (Barracuda)
```

### 2.1 Data
- **Input**: Hand joint positions (from XR Hands) as float vector
- **Labels**: GestureType (None, Pinch, Fist, OpenPalm, Point, Thrust, Shield)
- **Collect**: Play in VR, record poses + manual labels, or use HandPoseStub + keyboard labels

### 2.2 Training (Python)
- **Model**: Small MLP or 1D CNN (input: 21 joints × 3 coords = 63 floats)
- **Framework**: PyTorch or scikit-learn
- **Export**: ONNX format for Unity Barracuda

### 2.3 Unity Integration
- **Package**: Add `com.unity.barracuda` to Packages/manifest.json
- **Script**: `MLGestureRecognizer.cs` – loads ONNX, runs inference on HandPoseData
- **Swap**: Assign MLGestureRecognizer to GestureTrainingStation instead of RuleBasedGestureRecognizer

### 2.4 Scripts to Add
- `ml-training/gesture_collect.py` – collect + label hand poses
- `ml-training/gesture_train.py` – train classifier, export ONNX
- `Assets/Scripts/Training/MLGestureRecognizer.cs` – Barracuda inference

---

## Phase 3: AI Backend (Existing)

### Already in Project
- **AIClient** – HTTP to `localhost:8000`
- **LLMIntentExtractor** – voice/text → ChunkParameters
- **VoiceProcessor** – transcription

### Extend For
- **VFX generation**: POST prompt → receive image URL or base64
- **Gesture correction**: POST hand pose → receive "move hand up" hint
- **NPC dialogue**: LLM for dynamic responses

---

## Phase 4: Other AI/ML Components

| Component | Purpose | Status |
|-----------|---------|--------|
| **Procedural terrain from intent** | "Make it snowy" → adjust ChunkGenerator | LLMIntentExtractor exists |
| **Enemy behavior** | Simple behavior trees or ML policy | Not started |
| **Difficulty scaling** | ML predicts player skill, adjusts spawns | Not started |
| **Voice commands** | "Teleport to weapon room" | VRVoiceCommands exists |

---

## Quick Start

### VFX Training (Python)
```bash
cd ml-training
pip install torch pillow
python vfx_train.py --data ../unity-project/Assets/Art/Textures --output ./output
```

### Gesture Training (Python)
```bash
python gesture_collect.py  # Run while playing, press 1-6 to label
python gesture_train.py --export gesture_model.onnx
```

### Unity
1. Add Barracuda: `com.unity.barracuda` in manifest
2. Copy `gesture_model.onnx` to `Assets/StreamingAssets/`
3. Assign MLGestureRecognizer to GestureTrainingStation

---

## File Layout

```
aetheria-vr-rpg/
├── unity-project/
│   ├── Assets/
│   │   ├── Scripts/AI/          # AIClient, LLMIntentExtractor, VFXTextureRequest
│   │   ├── Scripts/Training/     # APIGestureRecognizer, RuleBasedGestureRecognizer
│   │   └── StreamingAssets/     # gesture_model.onnx (when using Barracuda)
│   └── ML_AI_ROADMAP.md
└── ml-training/
    ├── server.py                # Run: python server.py (gesture API)
    ├── vfx_train.py
    ├── gesture_collect.py
    ├── gesture_train.py
    ├── requirements.txt
```

## Quick Test (API Gesture)

1. Run: `cd ml-training && python server.py`
2. In Unity: Add APIGestureRecognizer to GestureStation, set Api Url = http://localhost:8000/gesture
3. Assign fallback = RuleBasedGestureRecognizer (used when server is down)
