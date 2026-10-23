# 🧯 PASS-AR: Mobile Fire Safety Training

> Learn how to use a fire extinguisher safely in your own room—no smoke, no mess, no stress.

---

## 💡 What is this?

Most people walk past red fire extinguishers in hallways and kitchens every single day, but very few have ever discharged one. When an emergency happens, hesitation costs valuable seconds. Discharging real fire extinguishers for practice isn’t practical—they're expensive, messy, and require professional facilities.

**PASS-AR** turns your smartphone into an interactive safety training tool using Augmented Reality. It places a realistic virtual fire into your physical space and walks you through the life-saving **P.A.S.S.** firefighting technique step by step.

---

## 🎯 The P.A.S.S. Technique

Fire safety organizations worldwide teach the four-step **P.A.S.S.** method. In the app, you practice each step in real time:

1. **P — Pull the Pin:** Unlock the extinguisher before attempting to squeeze the lever.
2. **A — Aim Low:** Aim the nozzle directly at the **base** of the fire where the fuel is—not at the rising flames. The app guides your crosshair and tells you if you're too close or too far away.
3. **S — Squeeze:** Press and hold down the lever to release the extinguishing spray. Releasing the lever stops the spray.
4. **S — Sweep:** Sweep the nozzle smoothly side to side across the base of the fire until every flame is extinguished.

---

## ✨ Features

- **Quick Room Setup:** Scan your floor or any flat ground surface with your camera, tap to place the fire hazard, and begin training immediately.
- **Smart Distance & Aim Feedback:** Real-time tips warn you if you're standing too close (danger zone), too far away (ineffective spray), or aiming at the wrong spot.
- **Interactive Extinguisher:** Features an on-screen pin-pull mechanic and a realistic hold-to-squeeze button that simulates the physical tension of a real handle.
- **Score & Performance Review:** After putting out the fire, see your completion time, mistakes made (such as trying to spray before pulling the pin), and your total safety score.
- **Practice Anytime in the Editor:** Built with XR simulation support, allowing testing and walkthroughs on a PC without needing a mobile device.

---

## 📱 How to Play on Android

1. Locate the built APK file in [`Builds/PASS-AR.apk`](Builds/PASS-AR.apk).
2. Install it on your Android phone (or install via `adb install Builds/PASS-AR.apk`).
3. Launch the app and allow camera permissions when prompted.
4. Point your camera towards the floor and slowly pan around to detect a surface.
5. Tap the screen to place the fire bin and follow the on-screen instructions!

---

## 🛠️ Built With

- **Engine:** Unity (Unity 6 / Universal Render Pipeline)
- **AR Tech:** AR Foundation & Google ARCore
- **Platform:** Android (Portrait orientation, optimized for one-handed and two-handed mobile use)

---

## 📁 Project Structure (At a Glance)

- **`Assets/PassAR/Scripts/`**
  - **`AR/`** — Handles camera permission, floor detection, and tap-to-place anchoring.
  - **`Core/`** — Coordinates the overall simulation, timer, and score calculation.
  - **`Tasks/`** — Step-by-step logic for Pull, Aim, Squeeze, and Sweep.
  - **`Entities/`** — Fire hazard behavior, flame intensity, and extinguisher spray physics.
  - **`UI/`** — Menus, instructions banner, reticle feedback, and end-of-round scorecards.
  - **`Audio/`** — Sound effects for pin pulling, pressurized spray, flame crackle, and victory cues.
- **`Assets/Scenes/PassAR.unity`** — The complete ready-to-play training scene.
