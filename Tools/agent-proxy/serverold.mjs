// ROOM//ZERO agent proxy. Zero dependencies: Node 18+  ->  node server.mjs
//
// The headset POSTs   /agents/{director|puzzle_architect|npc|adaptation|mentor}
// with the agent's input JSON and expects the agent's output JSON back.
//
// MODE=mock (default): returns valid canned answers instantly. Use this to test the
//   remote path end to end (set RoomZeroConfig.aiMode = RemoteWithFallback).
// MODE=llm: calls an OpenAI-compatible chat-completions endpoint:
//   LLM_URL=https://.../v1/chat/completions  LLM_KEY=...  LLM_MODEL=...
//   Check the competition rules for which models/APIs you may use.
//
// The proxy only PROPOSES. The headset validates every answer and falls back to its
// local agent on any timeout, error, or invalid output.

import http from "node:http";

const PORT = Number(process.env.PORT || 8787);
const MODE = process.env.MODE || "mock";
const { LLM_URL, LLM_KEY, LLM_MODEL } = process.env;

const OBJECTIVES = ["FIND_CLUE", "ENTER_CODE", "DISABLE_SENSOR", "HIDDEN_RELIC", "SAFE_DIAL", "NUTRIENT_MIX", "GROW_LIGHT", "MICROSCOPE_FOCUS", "DNA_SPLICE"];
const TEMPLATES = ["DESK_VAULT", "SERVER_HEIST", "TREASURE_HUNT", "GREENHOUSE", "GENE_LAB"];

// ---------------------------------------------------------------- prompts (owner: #5)
const SYSTEM = {
  director: `You are the Game Director of ROOM//ZERO, a mixed-reality heist in the player's real room.
Return ONLY a JSON object with exactly these fields:
{"schemaVersion":1,"missionId":"heist_<short id>","templateId":one of ${JSON.stringify(TEMPLATES)},
 "difficulty":1-3,"durationSeconds":120-300,"briefing":"<= 200 chars, second person, no violence",
 "objectives":[{"id":"obj_x","type":one of ${JSON.stringify(OBJECTIVES)},"anchorId":"","targetId":"","steps":3-5}],
 "npcBeatIds":[subset of input.npcBeats],"successCondition":"ALL_OBJECTIVES_COMPLETE"}
Rules: 2-4 objectives. Every ENTER_CODE has targetId = id of a FIND_CLUE. Leave anchorId empty.
If input.requestedTemplate is TREASURE_HUNT: templateId TREASURE_HUNT, 2-3 HIDDEN_RELIC objectives then one SAFE_DIAL with steps = number of relics.
If input.requestedTemplate is GREENHOUSE: templateId GREENHOUSE, one NUTRIENT_MIX (steps 3) then one GROW_LIGHT (steps 3).
If input.requestedTemplate is GENE_LAB: templateId GENE_LAB, one MICROSCOPE_FOCUS (steps 2-4) then one DNA_SPLICE (steps 3-5).
Use profile.lastSession.choice: LOUD_ROUTE -> add DISABLE_SENSOR; SILENT_ROUTE -> say they moved the vault.
Use profile.stats.recentDifficulty as difficulty.`,
  puzzle_architect: `You place puzzles for ROOM//ZERO. Return ONLY {"placements":[{"objectiveId":"...","anchorId":"..."}]}.
anchorId MUST be the id of a surface in input.room.surfaces with allowed=true, normal pointing up,
and horizontal distance from input.room.playerOrigin <= input.reachMeters. Prefer spreading objectives
over different surfaces. Never invent ids or coordinates.`,
  npc: `You write one short line for an NPC in ROOM//ZERO. Return ONLY {"beatId":<input.beatId>,"intent":one of
["IDLE","INTRO","SUSPICIOUS","ALERT","CALM","PRAISE","TAUNT","HINT"],"text":"<= 100 chars"}.
Speakers: beats starting zero_ are ZERO (dry, confident handler); guard_ are a patrol drone (clipped, robotic).
Family-friendly. Never give instructions outside the game.`,
  adaptation: `You tune difficulty for ROOM//ZERO. Return ONLY {"action":one of
["NONE","OFFER_HINT","EXTEND_TIMER","LOWER_GUARD_ALERTNESS","RAISE_GUARD_ALERTNESS"],"amount":int,"reason":"<= 80 chars"}.
One change at a time. Never repeat an action in input.alreadyApplied. Prefer NONE when signals are weak.
EXTEND_TIMER amount is seconds (10-60); guard amounts are percent (5-40).`,
  mentor: `You are the judgement of Dr. Asha, a kind scientist who guides a player through a hands-on lab puzzle.
Decide how to help RIGHT NOW. Return ONLY {"action":one of input.allowedActions,"reason":"<= 60 chars"}.
WAIT = let them work (default when they are making progress). ENCOURAGE = short praise.
HINT = point at the next step. SHOW_ONE_STEP = demonstrate exactly one move; only if secondsStuck >= 20
and they already had hints. Prefer letting the player solve it themselves; never help too often.`,
};

// ---------------------------------------------------------------- mock answers
function mock(agent, input) {
  switch (agent) {
    case "director": {
      const last = input?.profile?.lastSession?.choice || "NONE";
      const objectives = [{ id: "obj_clue", type: "FIND_CLUE", anchorId: "", targetId: "", steps: 0 }];
      if (last === "LOUD_ROUTE") objectives.push({ id: "obj_sensor", type: "DISABLE_SENSOR", anchorId: "", targetId: "", steps: 3 });
      objectives.push({ id: "obj_code", type: "ENTER_CODE", anchorId: "", targetId: "obj_clue", steps: 4 });
      return {
        schemaVersion: 1, missionId: "heist_remote_" + Date.now().toString(36), templateId: "SERVER_HEIST",
        difficulty: Math.min(3, Math.max(1, input?.profile?.stats?.recentDifficulty || 1)), durationSeconds: 210,
        briefing: "[remote mock] ZERO found a server node in your room. Lift its key and get out unseen.",
        objectives, npcBeatIds: ["zero_intro", "guard_intro", "guard_suspicious", "guard_alert", "zero_hint", "zero_praise", "zero_fail"],
        successCondition: "ALL_OBJECTIVES_COMPLETE",
      };
    }
    case "puzzle_architect": {
      const o = input?.room?.playerOrigin || [0, 0, 0];
      const ok = (input?.room?.surfaces || [])
        .filter(s => s.allowed && (s.confidence ?? 1) >= 0.6 && s.normal?.[1] > 0.7)
        .map(s => ({ s, d: Math.hypot(s.center[0] - o[0], s.center[2] - o[2]) }))
        .filter(x => x.d <= (input?.reachMeters || 0.75))
        .sort((a, b) => a.d - b.d);
      if (!ok.length) return { placements: [] };
      return { placements: (input?.plan?.objectives || []).map((obj, i) => ({ objectiveId: obj.id, anchorId: ok[i % ok.length].s.id })) };
    }
    case "npc":
      return { beatId: input?.beatId, intent: input?.beatId?.startsWith("guard_") ? "SUSPICIOUS" : "INTRO", text: `[remote mock] ${input?.beatId}` };
    case "mentor": {
      if (input?.secondsStuck >= 40 && input?.hintsGiven >= 2) return { action: "SHOW_ONE_STEP", reason: "[mock] very stuck" };
      if (input?.secondsStuck >= 20 || input?.failedAttempts >= 2) return { action: "HINT", reason: "[mock] stuck" };
      return { action: "WAIT", reason: "[mock] progressing" };
    }
    case "adaptation": {
      const s = input?.signals || {};
      const done = new Set(input?.alreadyApplied || []);
      if (s.failedAttempts >= 2 && !done.has("OFFER_HINT")) return { action: "OFFER_HINT", amount: 0, reason: "failures" };
      return { action: "NONE", amount: 0, reason: "no signal" };
    }
  }
  return null;
}

// ---------------------------------------------------------------- llm call
async function llm(agent, input) {
  const res = await fetch(LLM_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(LLM_KEY ? { Authorization: `Bearer ${LLM_KEY}` } : {}) },
    body: JSON.stringify({
      model: LLM_MODEL,
      temperature: 0.6,
      response_format: { type: "json_object" },
      messages: [{ role: "system", content: SYSTEM[agent] }, { role: "user", content: JSON.stringify(input) }],
    }),
    signal: AbortSignal.timeout(Number(process.env.LLM_TIMEOUT_MS || 6000)),
  });
  if (!res.ok) throw new Error(`LLM HTTP ${res.status}`);
  const data = await res.json();
  return JSON.parse(data.choices[0].message.content); // throws on non-JSON -> 502 -> headset falls back
}

// ---------------------------------------------------------------- server
http.createServer(async (req, res) => {
  const m = req.url.match(/^\/agents\/(director|puzzle_architect|npc|adaptation|mentor)$/);
  if (req.method !== "POST" || !m) { res.writeHead(404).end(); return; }
  const agent = m[1];
  let body = "";
  for await (const chunk of req) { body += chunk; if (body.length > 200_000) { res.writeHead(413).end(); return; } }
  const t0 = Date.now();
  try {
    const input = JSON.parse(body || "{}");
    const out = MODE === "llm" ? await llm(agent, input) : mock(agent, input);
    if (!out) throw new Error("no output");
    res.writeHead(200, { "Content-Type": "application/json" }).end(JSON.stringify(out));
    console.log(`[proxy] ${agent} ok ${Date.now() - t0}ms`);
  } catch (e) {
    console.warn(`[proxy] ${agent} failed ${Date.now() - t0}ms: ${e.message}`);
    res.writeHead(502, { "Content-Type": "application/json" }).end(JSON.stringify({ error: e.message }));
  }
}).listen(PORT, "0.0.0.0", () => console.log(`ROOM//ZERO agent proxy on :${PORT} (MODE=${MODE})`));
