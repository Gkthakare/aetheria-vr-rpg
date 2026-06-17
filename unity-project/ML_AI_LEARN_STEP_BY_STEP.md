# Aetheria VR – ML + AI: Step-by-Step Learning Guide

Follow these steps in order. Each section builds on the previous one.

---

## Part 1: Understand the Flow

### What You Have
- **Unity**: Hand pose data (from controllers or XR Hands) → needs to become a "gesture" (Pinch, Fist, etc.)
- **Unity**: Wants VFX textures (fire, smoke) → can come from AI-generated images

### Two Ways to Use ML
1. **API (HTTP)**: Unity sends data to a Python server → Python runs ML → returns result. No extra Unity packages.
2. **On-device (Barracuda)**: Train in Python, export ONNX → Unity loads and runs the model locally. Needs Barracuda package.

We start with **API** so you can learn without changing Unity packages.

---

## Part 2: Set Up Python (One-Time)

### Step 2.1 – Install Python
1. Download Python 3.10+ from https://www.python.org/downloads/
2. During install, **check "Add Python to PATH"**
3. Open a new **PowerShell** or **Command Prompt**
4. Run: `python --version`  
   - You should see something like `Python 3.11.x`

### Step 2.2 – Create a Virtual Environment (Recommended)
```powershell
cd C:\Users\intru\aetheria-vr-rpg\aetheria-vr-rpg
python -m venv venv
.\venv\Scripts\Activate.ps1
```
(If you get an error about execution policy, run: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`)

You should see `(venv)` at the start of your prompt.

### Step 2.3 – Install Dependencies
```powershell
cd ml-training
pip install -r requirements.txt
```
Wait for it to finish. You now have torch, numpy, etc.

---

## Part 3: Run the Gesture API Server

### Step 3.1 – Start the Server
```powershell
cd C:\Users\intru\aetheria-vr-rpg\aetheria-vr-rpg\ml-training
python server.py
```

You should see:
```
Aetheria ML API running at http://localhost:8000
  POST /gesture - hand pose -> gesture
  POST /generate_vfx - prompt -> image (Stable Diffusion)
```

**Leave this window open.** The server must stay running.

### Step 3.2 – Test the Server (Optional)
Open a **new** PowerShell window and run:
```powershell
curl -X POST http://localhost:8000/gesture -H "Content-Type: application/json" -d "{\"pinch\":0.9,\"grip\":0,\"lx\":0,\"ly\":0,\"lz\":0}"
```

You should get a response like `{"gesture": "pinch"}`.  
- `pinch` > 0.7 → "pinch"  
- `grip` > 0.7 → "fist"  
- `grip` < 0.2 → "open"

---

## Part 4: Connect Unity to the API

### Step 4.1 – Add APIGestureRecognizer
1. Open Unity and your **HelloVR** scene
2. Go to the **Training** room (or create a test scene with GestureStation)
3. Select **GestureStation** (under TrainingContent, or create one)
4. In the Inspector, click **Add Component**
5. Search for **API Gesture Recognizer** → add it

### Step 4.2 – Configure APIGestureRecognizer
1. **Api Url**: `http://localhost:8000/gesture`  
   (Use `127.0.0.1` instead of `localhost` if Unity on Android can’t reach it)
2. **Timeout Seconds**: `0.5`
3. **Cache Seconds**: `0.1`
4. **Fallback Recognizer**: Drag your **RuleBasedGestureRecognizer** component here  
   (So when the server is off, it still works with rules)

### Step 4.3 – Wire It to GestureTrainingStation
1. Select **GestureStation**
2. In **Gesture Training Station**, find **Recognizer Component**
3. Drag the **APIGestureRecognizer** component into that slot  
   (Instead of RuleBasedGestureRecognizer)

### Step 4.4 – Test
1. Make sure `python server.py` is still running
2. Press **Play** in Unity
3. Go to the Training room
4. Use keys **1** (Pinch), **2** (Fist), **3** (Open) – HandPoseStub sends data
5. The API receives it and returns the gesture. You should see the same behavior, but now it goes through Python.

**Note**: On a real Quest build, `localhost` points to the headset, not your PC. For device testing, run the server on your PC and use your PC’s IP (e.g. `http://192.168.1.5:8000/gesture`). Ensure Quest and PC are on the same Wi‑Fi.

---

## Part 4.5: VFX Textures with Stable Diffusion

### Step 4.5.1 – Install VFX Dependencies
```powershell
cd ml-training
pip install diffusers transformers accelerate
```
First run downloads ~4GB model. GPU recommended (NVIDIA CUDA).

### Step 4.5.2 – Test VFX Generation
With `python server.py` running, in a new terminal:
```powershell
curl -X POST http://localhost:8000/generate_vfx -H "Content-Type: application/json" -d "{\"prompt\":\"photorealistic mountain texture\",\"width\":256,\"height\":256}"
```
You should get JSON with `"image": "<base64>"`. First run may take 1–2 minutes (model load).

### Step 4.5.3 – Use in Unity
1. Add **VFXTextureRequest** to a GameObject
2. Assign a **Target Renderer** (e.g. a plane or terrain)
3. Call from script:
```csharp
GetComponent<VFXTextureRequest>().RequestTexture(
    "photorealistic mountain texture",
    tex => Debug.Log("Texture received!"),
    err => Debug.LogError(err)
);
```
4. Set **Api Url** to `http://localhost:8000/generate_vfx` (or your PC IP for Quest)

### Example Prompts for Realistic Assets
- `photorealistic mountain rock texture, detailed`
- `realistic water surface, reflections, 4k`
- `photorealistic forest tree bark texture`
- `realistic sky with clouds, blue`

---

## Part 4.6: AI 3D Models for Infinite Dungeon (Shap-E)

### Best for Infinite Dungeon
**Shap-E** (text-to-3D) is ideal: one prompt → 3D mesh, uses diffusers (already installed). TripoSR is image-to-3D and needs SD first.

### Step 4.6.1 – Test 3D Generation
With `python server.py` running:
```powershell
curl -X POST http://localhost:8000/generate_3d -H "Content-Type: application/json" -d "{\"prompt\":\"fantasy dungeon pillar\"}"
```
First run downloads ~2GB Shap-E model. Returns `{"obj": "<base64 OBJ>"}`.

### Step 4.6.2 – Use in Unity (ChunkManager)
1. Create empty GameObject, add **ModelPool** and **Model3DRequest**
2. Set Model3DRequest **Api Url** to `http://localhost:8000/generate_3d` (or PC IP for Quest)
3. Assign **Model Pool** to ChunkManager’s **Model Pool** slot
4. Set **AI Decor Chance** (e.g. 0.15) – fraction of grass/forest tiles that get AI decor
5. ModelPool preloads at startup; ChunkManager spawns trees/rocks from the pool

### Example Prompts for Dungeon Decor
- `fantasy dead tree stump`, `small rock boulder`
- `fantasy dungeon pillar stone`, `medieval torch holder`

---

## Part 4.7: Terrain Textures + Voice/Message Input

### Generate Realistic Terrain (Mountain, Water, Sand, etc.)
1. Add **TerrainTextureGenerator** to a GameObject
2. Assign **ChunkManager** and **VFXTextureRequest**
3. Call `GenerateForTerrain(TileType.Mountain)` or use voice

### Voice / Message Input
1. Add **GenerationInputController** (assign TerrainTextureGenerator, VoiceProcessor)
2. **Voice**: Say "generate mountain", "generate water", "generate sand with golden desert"
3. **Custom prompt**: "generate mountain with volcanic rock"
4. **All terrain**: "generate all terrain" or "generate all"

### Unity Setup
- `TerrainTextureGenerator` → ChunkManager, VFXTextureRequest
- `GenerationInputController` → TerrainTextureGenerator, VoiceProcessor
- `VoiceIntegrationSystem` → GenerationInputController (auto-finds)

---

## Part 4.8: Anime-Style Training

See **ML_ANIME_TRAINING_ROADMAP.md** for:
- Data collection for anime textures
- LoRA fine-tuning options
- Using existing anime models (Anything V5, Counterfeit)

---

## Part 5: Replace Rules with a Real ML Model (Python)

### Step 5.1 – Understand the Data
The server receives: `{pinch, grip, lx, ly, lz}`.  
A full ML model would use all 21 hand joints (63 floats). For learning, we use pinch + grip.

### Step 5.2 – Add a Simple Classifier
1. Open `ml-training/server.py` in a text editor
2. At the top, add:
```python
from sklearn.linear_model import LogisticRegression
import numpy as np

# Simple model: [pinch, grip] -> class
model = LogisticRegression(max_iter=1000)
# Train on dummy data for demo
X = np.array([[0.9, 0.1], [0.1, 0.9], [0.1, 0.1], [0.5, 0.5]])
y = np.array([1, 2, 3, 0])  # 0=none, 1=pinch, 2=fist, 3=open
model.fit(X, y)
```

3. In `handle_gesture`, replace the rule block with:
```python
pred = model.predict([[pinch, grip]])[0]
gestures = ["none", "pinch", "fist", "open"]
gesture = gestures[pred]
```

4. Save, restart `python server.py`, and test again.

You’ve just swapped rule-based logic for a trained model (even if the data is dummy).

---

## Part 6: Train a Real Gesture Model (With Real Data)

### Step 6.1 – Collect Data
1. Add logging to Unity: when HandPoseStub or XR Hands sends a pose, also log it to a file or send it to a collector script.
2. Or: extend `gesture_collect.py` to run an HTTP server that receives poses from Unity.
3. In VR (or with HandPoseStub), perform each gesture many times (e.g. 50+ per gesture).
4. Save as CSV: `label,pinch,grip,joint1_x,...`

### Step 6.2 – Train
1. Load the CSV in Python
2. Split into X (features) and y (labels)
3. Train: `model.fit(X, y)`
4. Save: `import joblib; joblib.dump(model, "gesture_model.pkl")`

### Step 6.3 – Use in Server
```python
import joblib
model = joblib.load("gesture_model.pkl")
# In handle_gesture:
features = [[pinch, grip, ...]]  # match your CSV columns
pred = model.predict(features)[0]
```

---

## Part 7: VFX Image Training (Overview)

### Step 7.1 – Gather Images
1. Create folders: `ml-training/data/fire/`, `data/water/`, `data/smoke/`, `data/magic/`
2. Add 20–50 images per folder (from AI tools, screenshots, or your existing textures)
3. Resize to 256×256 or 512×512

### Step 7.2 – Train (Concept)
- **Option A**: Fine-tune Stable Diffusion with LoRA on your VFX images
- **Option B**: Train a small GAN (e.g. DCGAN) per category
- **Option C**: Use an image-to-image model (e.g. ControlNet) with your style

### Step 7.3 – Export for Unity
- Save generated images to `output/`
- Copy into `unity-project/Assets/Art/Textures/ML_Generated/`
- Create materials in Unity and assign them

### Step 7.4 – Runtime (Optional)
- Add a `/generate_vfx` endpoint in `server.py` that calls your trained model
- Unity’s **VFXTextureRequest** sends a prompt → receives image → applies to material

---

## Part 8: On-Device ML (Barracuda) – Optional

### Step 8.1 – Add Barracuda
1. In Unity: **Window → Package Manager**
2. **Add package by name** → `com.unity.barracuda`
3. Install

### Step 8.2 – Export ONNX from Python
```python
# In gesture_train.py, after training:
torch.onnx.export(model, dummy_input, "gesture_model.onnx")
```

### Step 8.3 – Load in Unity
1. Copy `gesture_model.onnx` to `Assets/StreamingAssets/`
2. Create `MLGestureRecognizer.cs` that uses `Unity.Barracuda` to load and run the model
3. No server needed; runs on the device

---

## Checklist

- [ ] Python installed, venv created, requirements installed
- [ ] `python server.py` runs and responds to curl
- [ ] APIGestureRecognizer added to GestureStation, API URL set
- [ ] Fallback recognizer assigned
- [ ] Tested in Play mode with HandPoseStub (keys 1/2/3)
- [ ] (Optional) Replaced rules with sklearn model in server.py
- [ ] (Optional) Collected real gesture data and trained
- [ ] (Optional) VFX images collected and training pipeline run
- [ ] (Optional) Barracuda added and ONNX model loaded

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `python` not found | Add Python to PATH or use `py` instead of `python` |
| Unity can't reach localhost | Use `127.0.0.1` or your PC's IP |
| Server returns "none" always | Check pinch/grip values; rules: pinch>0.7, grip>0.7, grip<0.2 |
| Quest can't reach server | Run server on PC, use PC's IP, same Wi‑Fi |
| APIGestureRecognizer not found | Ensure script is in Assets/Scripts/Training/ |
