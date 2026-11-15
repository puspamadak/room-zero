# Room ZERO Gameplay UI/UX

## Features
- 01 — Room-first HUD
  Compact mission status, a nearby objective marker, and subtle interaction hints.
- 02 — Object inspection
  Evidence cues, observed versus inferred information, and consequential approach choices.
- 03 — Contact interaction
  A character moment with branching responses and a visible consequence for mission state.
- 04 — Session recap
  A short mission log showing saved choices and a return-to-room state.
- 05 — Smooth transition
  Subtle animations keep users focussed and is preferable to all.
- 06 — Transparency and subtle backdrop blur
  To make the UI blend into the surroundings while keeping distraction-free as much as possible. The backdrop blur improves foreground text contrast.

## Design decisions
- Real room first: no full-screen virtual dashboard or replacement environment.
- Hands-first interaction: look and pinch are primary, with voice and fallback controls represented in the interaction guidance.
- Spatially restrained: small translucent panels, subtle anchors, low visual clutter, and a muted charcoal/cyan palette.
- Choices have consequences: the selected approach changes the mission feedback, while the contact response is carried into the session recap.
- Safety-aware: the pause state keeps the room visible and reinforces room-boundary awareness.

## Components
- HUD: boxes for the objectives and missions shown in the upper left & right corners.
- Diaog box: used for introducing or explaining the user about something.
- Toast: used for showing temporary messages after an action in a distraction-free manner. In the demo, double-click anywhere to show a sample toast message.
- Wrist menu: common and important actions such as settings should be present here. Always available to the user in their hand. Shown as "Menu" on the user's hand in demo, but will be replaced by a wristband in the actual game.
- Message box: help messages will be displayed in the bottom left corner.

## Main experience of the demo
1. **Boot sequence** — animated system initialization with progress stages for spatial systems, room boundaries, hand input, and mission profile. The user can skip the intro. In the demo, boot sequence is not automatically skipped.
2. **Field brief** — explains room awareness, using the real room, and interacting by looking/pinching or using voice when hand tracking is unavailable. Includes a room-check status and Enter Mission action.
3. **Mission HUD** — shows mission progress/state, an objective card, a central spatial anchor, and an Inspect Object action.
4. **Objective inspection** — presents a console access point and two evidence cues:
   - Panel seam — observed.
   - Signal pulse — inferred.
   The user chooses quiet access (slower, lower risk) or forcing the panel (faster, likely to trigger a sweep).
5. **Local contact** — a contact warns that power spikes can trigger a sweep. The user can trust the warning or ask for more context.
6. **Session log** — summarizes the selected approach and contact response, indicates the session profile was updated, and offers a return-to-room action.

## Wrist Link menu
The wrist-oriented menu opens four utility panels:
- **Inventory:** signal trace (secured evidence) and Access shim (quiet-access tool).
- **Settings:** toggles for spatial cues, wrist haptics, voice fallback, and reduced motion. The panel describes these as local preferences.
- **Pause:** pause in place, keep the real room visible, show mapped room boundary guidance, and resume or end the session.
- **Controls:** quick reference for look + pinch, voice commands, and dwell/controller fallback where supported.

## Design principles visible in the prototype
- **Room-first:** repeatedly reminds users to stay aware of physical surroundings and avoid blocking real-world surfaces.
- **Spatially anchored cues:** a central anchor and objective cues suggest placement in the room rather than replacing the environment.
- **Evidence confidence:** observed and inferred evidence are explicitly distinguished.
- **Choice-driven state:** approach and contact response affect the displayed recap and mission state.
- **Comfort and fallback:** settings expose reduced motion, spatial cues, haptics, and voice fallback; controls mention dwell/controller fallback where supported.