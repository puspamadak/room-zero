#!/usr/bin/env bash
# One command to turn on ROOM//ZERO's real AI on your Mac:
# local Llama 3.2 (Ollama) + the agent proxy the game talks to.
#   cd tools/agent-proxy && ./start-local-ai.sh
# Then in Unity: RoomZeroConfig > AI Mode = RemoteWithFallback, Agent Proxy Url = http://127.0.0.1:8787
# (on a real Quest use your Mac's Wi-Fi IP instead of localhost; the script prints it).
set -e
MODEL="${LLM_MODEL:-llama3.2}"
cd "$(dirname "$0")"

command -v node >/dev/null || { echo "Node.js 18+ is needed: https://nodejs.org (or: brew install node)"; exit 1; }
command -v ollama >/dev/null || { echo "Ollama is needed: https://ollama.com/download (or: brew install ollama)"; exit 1; }

if ! curl -s http://localhost:11434/api/tags >/dev/null; then
  echo "Starting Ollama..."
  (ollama serve >/tmp/ollama.log 2>&1 &)
  for i in $(seq 1 20); do curl -s http://localhost:11434/api/tags >/dev/null && break; sleep 0.5; done
fi
ollama list | grep -q "^$MODEL" || { echo "Downloading $MODEL (about 2 GB, first time only)..."; ollama pull "$MODEL"; }

# Warm the model up so the first in-game decision is fast.
curl -s http://localhost:11434/api/generate -d "{\"model\":\"$MODEL\",\"prompt\":\"ok\",\"stream\":false,\"keep_alive\":\"60m\"}" >/dev/null || true

IP=$(ipconfig getifaddr en0 2>/dev/null || hostname -I 2>/dev/null | awk '{print $1}' || echo "your-mac-ip")
echo ""
echo "ROOM//ZERO AI is ON  (model: $MODEL, runs on this Mac, no internet or API key needed)"
echo "  Unity Editor / Meta XR Simulator: Agent Proxy Url = http://127.0.0.1:8787"
echo "  Real Quest on the same Wi-Fi:     Agent Proxy Url = http://$IP:8787"
echo "  In the game, ZERO'S MIND shows 'AI' next to each decision the model made."
echo ""
MODE=llm LLM_URL="http://localhost:11434/v1/chat/completions" LLM_MODEL="$MODEL" LLM_TIMEOUT_MS="${LLM_TIMEOUT_MS:-20000}" exec node server.mjs
