# Part 4: Connect Unity to the API – Detailed Steps

Make sure **Part 3** is done: `python server.py` is running in a terminal and you've tested it with the PowerShell command.

---

## Step 4.1 – Open Unity and Your Scene

1. Open **Unity** and load your project.
2. Open the **HelloVR** scene (or whichever scene you use).
3. In the **Hierarchy** (left panel), look for:
   - **WorldManager** (which may spawn rooms at runtime), or
   - **WeaponRoom**, **StartCube**, etc.

---

## Step 4.2 – Find or Create GestureStation

You need a **GestureStation** object. It can come from:

### Option A – You Have a Training Room
1. Press **Play** in Unity.
2. In the Hierarchy, expand **WorldManager**.
3. Find a child named like **Room_Training_xxxx**.
4. Expand it and look for **TrainingContent**.
5. Under **TrainingContent**, find **GestureStation**.
6. **Stop Play** – runtime objects disappear. So we need to set this up via a prefab or a persistent object.

### Option B – Create a Simple Test Setup (Recommended)
1. **Stop Play** if it’s running.
2. In the Hierarchy, right‑click → **Create Empty**.
3. Rename it to **GestureTest**.
4. With **GestureTest** selected, in the Inspector click **Add Component**.
5. Add these components one by one (search for each):
   - **Hand Pose Stub**
   - **Rule Based Gesture Recognizer**
   - **Training VFX**
   - **Gesture Training Station**
   - **API Gesture Recognizer**
6. Position **GestureTest** where you can reach it (e.g. near WeaponRoom):  
   - Select GestureTest → in **Transform** set Position to `0, 1.2, 5` (or near your player).

---

## Step 4.3 – Add APIGestureRecognizer (If Not Already Added)

1. Select **GestureTest** (or **GestureStation** if you’re using that).
2. In the Inspector, check if **API Gesture Recognizer** is already there.
3. If not: **Add Component** → search **API Gesture Recognizer** → add it.

---

## Step 4.4 – Configure APIGestureRecognizer

With **GestureTest** (or **GestureStation**) selected:

1. In the **API Gesture Recognizer** component, set:
   - **Api Url**: `http://localhost:8000/gesture`  
     (Or `http://127.0.0.1:8000/gesture` if localhost causes issues.)
   - **Timeout Seconds**: `0.5`
   - **Cache Seconds**: `0.1`
   - **Fallback Recognizer**:  
     - In the Hierarchy, make sure the same object has **Rule Based Gesture Recognizer**.  
     - In the **Fallback Recognizer** slot, drag the **Rule Based Gesture Recognizer** component from the same object (or from another object that has it).

2. **Fallback** is used when the server is not reachable, so gesture recognition still works with rules.

---

## Step 4.5 – Wire APIGestureRecognizer to GestureTrainingStation

1. With **GestureTest** (or **GestureStation**) selected, find the **Gesture Training Station** component.
2. Find the field **Recognizer Component**.
3. Drag the **API Gesture Recognizer** component into that slot.  
   - To drag: click the small circle next to the field, or drag the component from the same object’s header in the Inspector.

4. The **Recognizer Component** should now show **API Gesture Recognizer (Script)**.

---

## Step 4.6 – Ensure HandPoseStub Is Present

1. On the same object (**GestureTest** or **GestureStation**), confirm **Hand Pose Stub** is attached.
2. **Hand Pose Stub** provides test input when you’re not in VR:
   - **1** = Pinch  
   - **2** = Fist  
   - **3** = Open Palm  

---

## Step 4.7 – Test in the Editor

1. Ensure **`python server.py`** is still running in your terminal.
2. In Unity, press **Play**.
3. Move the camera so you can see **GestureTest** (or stand in the Training room).
4. Press and hold **1** on the keyboard (simulates Pinch).
5. Hold for about 0.5 seconds.
6. You should see the **Training VFX** success effect (if configured) or at least no errors in the Console.
7. Try **2** (Fist) and **3** (Open Palm) as well.

---

## Step 4.8 – Verify the API Is Being Called

1. Look at the terminal where **`python server.py`** is running.
2. When you press **1**, **2**, or **3**, you should see log lines for each request, e.g.:
   ```
   [timestamp] "POST /gesture HTTP/1.1" 200 -
   ```
3. If you see these lines, Unity is successfully calling the API.

---

## Troubleshooting

| Problem | What to do |
|--------|------------|
| **"Recognizer Component" slot is empty** | Drag the **API Gesture Recognizer** component into it. Make sure you’re dragging the component, not the GameObject. |
| **No GestureStation / GestureTest** | Use Option B above to create **GestureTest** with all required components. |
| **Console error: "Connection refused"** | Start `python server.py` and leave it running. |
| **Console error: "localhost not found"** | Try `http://127.0.0.1:8000/gesture` instead. |
| **Nothing happens when pressing 1/2/3** | Confirm **Hand Pose Stub** is on the same object. Check that **Gesture Training Station** is enabled (checkbox on). |
| **Fallback Recognizer shows "Missing"** | Add **Rule Based Gesture Recognizer** to the same object and assign it to the Fallback slot. |

---

## Summary Checklist

- [ ] `python server.py` is running
- [ ] GestureTest (or GestureStation) exists with: HandPoseStub, RuleBasedGestureRecognizer, TrainingVFX, GestureTrainingStation, APIGestureRecognizer
- [ ] APIGestureRecognizer: Api Url = `http://localhost:8000/gesture`, Fallback = RuleBasedGestureRecognizer
- [ ] GestureTrainingStation: Recognizer Component = APIGestureRecognizer
- [ ] Press Play, press 1/2/3, see server logs and/or VFX
