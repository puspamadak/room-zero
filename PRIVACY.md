# Privacy Policy for ROOM//ZERO

**Last Updated:** November 18, 2026

## 1. Introduction

Welcome to **ROOM//ZERO** ("we," "our," or "us"). ROOM//ZERO is a Unity-based mixed-reality (MR) prototype developed for the Meta VR Hackathon 2026. Because this application relies on spatial computing, hand tracking, and passthrough technologies to create an interactive bio-secure laboratory experience, we want to be transparent about how device sensors and user data are handled during your session.

Please note that ROOM//ZERO is a non-commercial hackathon prototype intended for demonstration, testing, and evaluation purposes only.

---

## 2. Information We Access and Process

To deliver a room-scale mixed-reality experience, ROOM//ZERO interacts with specific hardware sensors and software frameworks provided by your XR headset (via Meta XR SDK and OpenXR) or desktop simulator.

### A. Spatial and Environmental Data (Passthrough & Room Mapping)
* **What is accessed:** Camera passthrough feeds and room boundary/mesh data used to anchor the virtual three-room scientific facility into your physical space.
* **How it is used:** This data is processed locally on your device in real time so virtual objects (such as the Seed Vault, Hydroponics Greenhouse, and Gene Lab fixtures) align accurately with your physical surroundings.
* **Storage:** We **do not** record, store, or transmit raw camera feeds, video, or detailed 3D scans of your physical room to external servers.

### B. Hand Tracking and Biometric Interaction Data
* **What is accessed:** Skeletal hand-tracking coordinates, finger-pinch states, and palm orientation (e.g., for the bio-secure entrance palm-scanner interaction and object manipulation).
* **How it is used:** Processed strictly in real time to translate your hand movements into in-game interactions. 
* **Storage:** The bio-secure "palm scanner" is a simulated gameplay mechanic. We **do not** capture, store, or verify real biometric templates, fingerprints, or identifiable hand geometry.

### C. AI and Remote-Agent Guidance Data
* **What is accessed:** In-game mission state, room progress, and interaction triggers associated with the guided NPC loop (Dr. Asha).
* **How it is used:** If AI or remote-agent support is enabled via the `RoomZeroConfig` asset, anonymized gameplay state data (such as current room number or puzzle status) may be processed to generate contextual hints and narrative guidance. No personally identifiable information (PII) is required or intentionally transmitted for this feature.

### D. Desktop Simulation Data
* **What is accessed:** Keyboard inputs and local system configuration when running the project via Unity Editor or the Meta XR Simulator on Windows or Apple Silicon macOS.
* **How it is used:** Used solely to simulate XR movement and puzzle inputs locally on your machine.

---

## 3. Data Storage and Retention

* **Local Execution:** ROOM//ZERO operates primarily as a local client application. Mission progress, room configurations (`RoomZeroConfig`), and fixture states exist temporarily in device memory during active gameplay.
* **No User Accounts:** The prototype does not require user registration, login credentials, or personal profiles.
* **No Persistent Tracking:** We do not use advertising identifiers, cross-app tracking, or persistent analytics cookies.

---

## 4. Third-Party Services and SDKs

ROOM//ZERO is built using third-party frameworks that have their own privacy practices:
* **Meta XR SDK & Meta Horizon OS:** Handles low-level hand tracking, passthrough rendering, and spatial anchors on Meta Quest devices. Data processed by the Meta runtime is governed by the [Meta Privacy Policy](https://www.meta.com/legal/privacy-policy/).
* **Unity Engine & OpenXR:** Provides the underlying runtime and cross-platform XR integration. governed by the [Unity Privacy Policy](https://unity.com/legal/privacy-policy).

If a developer or evaluator configures custom API keys for AI-assisted room guidance in `RoomZeroConfig`, any data sent to that AI provider is subject to that specific provider's privacy terms.

---

## 5. Children's Privacy

ROOM//ZERO is a hackathon prototype and is not directed toward children under the age of 13 (or the applicable age of digital consent in your jurisdiction). We do not knowingly collect personal information from children.

---

## 6. Security

Because ROOM//ZERO is distributed as source code for hackathon evaluation and local testing, security depends largely on the environment in which it is compiled and run. Developers running the project locally are responsible for safeguarding any API keys or local configuration files (`RoomZeroConfig`) they add to the project.

---

## 7. Changes to This Privacy Policy

As ROOM//ZERO is an active hackathon prototype, features and documentation may evolve. Any updates to how the application handles spatial or interaction data will be reflected in this document within the project repository.

---

## 8. Contact Information

If you have questions about this Privacy Policy or the ROOM//ZERO prototype, please open an issue or reach out via the official project repository:

* **Repository:** [https://github.com/puspamadak/room-zero](https://github.com/puspamadak/room-zero)
