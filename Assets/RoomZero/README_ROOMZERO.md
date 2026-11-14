# ROOM//ZERO template: quick start

1. Menu **RoomZero → Build Demo Scene**
2. Menu **RoomZero → Run Self-Tests** (expect 42 passed)
   Menu **RoomZero → Check Project Compatibility** (version / input / XR / Meta SDK / Android checks)
3. Press **Play** (use the **Game** tab, not the phone **Simulator** tab). **V** heist · **B** formula hunt (recover the Harvest Formula) · **1/2** silent/loud ·
   follow the orange **NEXT:** line and bouncing arrow · **F** face next target · click to poke ·
   **0-9 + Enter** type the code · **H** hint · **P** pause · **N** next · **T** tracking loss

Unity: any Unity 6 (6000.0 LTS ≥66f2, 6000.3 LTS, 6000.4–6000.6). On 6000.4.4+ use Meta XR SDK **v207+**.
Run **RoomZero → Check Project Compatibility** after opening; it lists exact fixes.

Meta XR Simulator (standalone app v207.1, Apple-silicon Mac or Windows): install Meta XR All-in-One SDK v207,
enable OpenXR + Meta XR feature group (Standalone and Android), delete Main Camera, add Camera Rig +
Passthrough + Hand Tracking + MR Utility Kit building blocks, **RoomZero → Enable Meta XR Integration**,
then Window → Meta → Meta XR Simulator → Activate and press Play. Aim a controller laser + trigger to press.
Step-by-step: docs/simulator-setup.md in the repo zip.

Full docs (architecture, contracts, team plan, device tests, demo script) and the agent proxy are in the
ROOM-ZERO repository zip: README.md, docs/, demo/, tools/agent-proxy/.

## Realistic visuals (v13)
- Glassware, bottles, plants, microscopes, monitors and furniture are built from real curved meshes in `Scripts/UI/Shapes.cs` (turned glass with liquid inside, rounded edges, curved leaves, see-through glass, brushed steel).
- Photo-real option: select `RoomZeroConfig` and drag any lab prefab into **Lab Environment Model** (and adjust **Lab Environment Scale**). It replaces the generated walls, benches and decor; the play tables stay so the mission still fits.
- The vault, formula vial and Harvest Formula jar also accept models in the **3D models** slots.

## 3-room campaign + Dr. Asha (v15)
- **Room 1, Seed Vault Lab:** find the formula fragments, open the seed vault.
- **Room 2, Hydroponics Greenhouse:** NUTRIENT MIX (match the N-P-K recipe drop by drop, then MIX) and GROW-LIGHT BALANCE (LIGHT / WATER / HEAT into the green band; lamps add heat, water cools).
- **Room 3, Gene Lab:** MICROSCOPE (focus each slide, tag HEALTHY or DISEASED) and GENE SPLICER (fill the gaps: A-T, G-C).
- **Dr. Asha** walks in through the door at the start of every room and solves each challenge step by step, explaining as she goes. Touch any control to take over; she steps back and watches, and resumes after 15 s idle. Desktop: **J** toggles her help.
- Desktop keys: **B** starts the quest, **N** goes to the next room after a win.

## v16: bio-secure entrance + real Dr. Asha
- **Room 1 entrance:** the lab starts dark behind frosted bio-secure doors. Put your hand on the palm scanner on the desk; it scans, says IDENTITY VERIFIED, the doors slide into the walls, the lights flicker on and Dr. Asha walks in through the doorway.
- **Dr. Asha** is now a real, motion-captured human (Microsoft Rocketbox "Medical_Female_02": white lab coat, real textures) with code-made spectacles. She walks, works at the controls, listens, talks, waves and cheers. Set up automatically by **RoomZero > Build Demo Scene** (or **RoomZero > Set Up Dr. Asha**). Licence: MIT, see `Characters/DrAsha/ROCKETBOX_LICENSE.md`. Credit: "Character: Microsoft Rocketbox Avatar Library (MIT)".
- **Same quality as before:** the look was tuned in the Built-in render pipeline project (from ROOM-ZERO.zip). A new "Universal 3D" project renders differently. For the identical look, import into that original project.
