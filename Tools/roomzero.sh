#!/usr/bin/env bash
PROJ="$HOME/ROOM-ZERO-v26"
PROXY="$HOME/Downloads/ROOM-ZERO 2/tools/agent-proxy"
PKG="$HOME/Downloads/RoomZero_v26_mr_window.unitypackage"
CFG="$PROJ/Assets/RoomZero/RoomZeroConfig.asset"
if [ "$1" = "ai" ]; then
  [ -f "$PROXY/start-local-ai.sh" ] || { echo "agent-proxy not found at: $PROXY"; exit 1; }
  cd "$PROXY" && chmod +x start-local-ai.sh && exec ./start-local-ai.sh
fi
ok(){ printf "  \033[32m✔\033[0m %s\n" "$1"; }
no(){ printf "  \033[31m✘\033[0m %s\n" "$1"; }
todo(){ printf "  \033[90m•\033[0m %s\n" "$1"; }
has(){ grep -q "$1" "$2" 2>/dev/null; }
echo "== ROOM//ZERO v26 check =="
echo "1. Tools"
command -v node >/dev/null && ok "Node.js $(node -v)" || no "Node.js missing"
command -v ollama >/dev/null && ok "Ollama installed" || no "ollama command missing"
curl -s --max-time 2 http://localhost:11434/api/tags >/dev/null && ok "Ollama running" || todo "Ollama not running (AI start starts it)"
ollama list 2>/dev/null | grep -q '^llama3.2' && ok "Llama 3.2 downloaded" || todo "Llama 3.2 not downloaded"
echo "2. Files"
[ -f "$PKG" ] && ok "unitypackage in Downloads" || no "unitypackage missing: $PKG"
[ -f "$PROXY/start-local-ai.sh" ] && ok "agent-proxy in Downloads/ROOM-ZERO 2" || no "agent-proxy missing: $PROXY"
echo "3. Unity project ($PROJ)"
[ -d "$PROJ/Assets" ] && ok "project exists" || no "project missing"
has universal "$PROJ/Packages/manifest.json" && ok "Universal 3D (URP)" || no "not URP"
has '"v26"' "$PROJ/Assets/RoomZero/Scripts/App/RoomZeroBootstrap.cs" && ok "v26 imported" || todo "not imported -> Assets > Import Package"
[ -f "$PROJ/Assets/RoomZero/Scenes/RoomZero_Main.unity" ] && ok "demo scene built" || todo "scene not built -> RoomZero > Build Demo Scene"
echo "4. AI settings"
if [ -f "$CFG" ]; then
  has 'aiMode: 1' "$CFG" && ok "Ai Mode = RemoteWithFallback" || todo "Ai Mode not RemoteWithFallback (or not saved: Cmd+S)"
  has 'agentProxyUrl: http://127.0.0.1:8787' "$CFG" && ok "Agent Proxy Url OK" || todo "Agent Proxy Url not http://127.0.0.1:8787"
  has 'agentTimeoutMs: 6000' "$CFG" && ok "Agent Timeout Ms = 6000" || todo "Agent Timeout Ms not 6000"
else todo "RoomZeroConfig not found (Build Demo Scene creates it)"; fi
curl -s --max-time 2 http://127.0.0.1:8787/health >/dev/null && ok "AI proxy running on :8787" || todo "AI proxy not running -> ~/roomzero.sh ai"
echo "5. Mixed reality (optional)"
M="$PROJ/Packages/manifest.json"
has com.unity.xr.openxr "$M" && ok "OpenXR" || todo "OpenXR -> RoomZero > Install XR Packages"
has com.meta.xr.sdk.core "$M" && ok "Meta XR Core SDK" || todo "Meta XR Core SDK"
has com.meta.xr.mrutilitykit "$M" && ok "Meta MR Utility Kit" || todo "Meta MR Utility Kit"
has com.meta.xr.simulator "$M" && ok "Meta XR Simulator" || todo "Meta XR Simulator"
has ROOMZERO_META_XR "$PROJ/ProjectSettings/ProjectSettings.asset" && ok "Meta XR Integration enabled" || todo "RoomZero > Enable Meta XR Integration"
echo "== done ==   ✔ ready   ✘ fix now   • later step"
