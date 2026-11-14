# ROOM//ZERO agent proxy: turn on real AI (Llama)

ZERO's agents (Director, Puzzle Architect, NPC, Adaptation, **Mentor = Dr. Asha's judgement**)
run on rules by default. Point the game at this proxy and they are decided by a language model instead.
Every model answer is still checked by the game (Mission Validator / mentor guardrail); anything slow,
wrong or offline falls back to the rules, so the game never breaks.

## Quickest: one command on your Mac
```
cd tools/agent-proxy && ./start-local-ai.sh
```
It starts Ollama, downloads Llama 3.2 the first time (~2 GB), warms it up and runs the proxy in LLM mode.
Leave that Terminal window open while you play / record. Then do step 3 below.

## Option A (free, offline, recommended for the demo): Llama on your laptop with Ollama
1. Install Ollama: https://ollama.com (Mac app), then in Terminal:
   `ollama pull llama3.2`
2. Start the proxy (Node 18+), from this folder:
   ```
   MODE=llm LLM_URL=http://localhost:11434/v1/chat/completions LLM_MODEL=llama3.2 node server.mjs
   ```
3. In Unity, select **RoomZeroConfig**:
   - **Ai Mode** = `RemoteWithFallback`
   - **Agent Proxy Url** = `http://127.0.0.1:8787` (desktop). On Quest: `http://<your Mac's Wi-Fi IP>:8787`
   - **Agent Timeout Ms** = `6000` (local models need a moment)
4. Press Play. The **ZERO'S MIND** panel (desktop: bottom right; headset / simulator: floating lower-left, poke MIND; `Z` toggles) shows **AI** next to every decision
   the model made, and **rules** where it fell back.

## Option B: hosted Llama / any OpenAI-compatible API
`MODE=llm LLM_URL=https://<provider>/v1/chat/completions LLM_KEY=<key> LLM_MODEL=<model> node server.mjs`
Keys stay on the proxy, never in the APK. Check the competition rules for allowed models/APIs.

## Test without any model
`node server.mjs` (MODE=mock) returns canned answers instantly, to test the remote path end to end.

## Quest notes
- Mac and Quest on the same Wi-Fi; allow incoming connections for `node` if macOS asks.
- Android blocks plain http by default: in Player Settings > Other, set **Allow downloads over HTTP** to *Always allowed* (desktop/dev builds only).
