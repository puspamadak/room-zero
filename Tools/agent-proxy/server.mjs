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

// ---------------------------------------------------------------- compact inputs
// Small local models (Llama 3.2 3B) are faster and far more accurate with short, focused inputs.
// The model sees only what its decision needs; geometry is pre-filtered into valid choices.
function validSurfaces(input) {
  const o = input?.room?.playerOrigin || [0, 0, 0];
  return (input?.room?.surfaces || [])
    .filter(s => s.allowed && (s.confidence ?? 1) >= 0.6 && (s.normal?.[1] ?? 0) > 0.7)
    .map(s => ({ id: s.id, type: s.type, distance: +Math.hypot(s.center[0] - o[0], s.center[2] - o[2]).toFixed(2) }))
    .filter(x => x.distance <= (input?.reachMeters || 0.75))
    .sort((a, b) => a.distance - b.distance);
}

function compact(agent, input) {
  switch (agent) {
    case "director": return {
      requestedTemplate: input?.requestedTemplate || "",
      lastChoice: input?.profile?.lastSession?.choice || "NONE",
      recentDifficulty: input?.profile?.stats?.recentDifficulty || 1,
      npcBeats: input?.npcBeats || [],
    };
    case "puzzle_architect": return {
      objectives: (input?.plan?.objectives || []).map(o => ({ id: o.id, type: o.type })),
      surfaces: validSurfaces(input),
    };
    case "npc": return {
      beatId: input?.beatId, missionState: input?.missionState, route: input?.route,
      guardAlertness: +(input?.guardAlertness || 0).toFixed(2),
    };
    default: return input;   // mentor, adaptation: already small
  }
}

const PROMPTS = {
  ...SYSTEM,
  director: `You are the Game Director of ROOM//ZERO, a family-friendly mixed-reality heist set in the player's real room.
Return ONLY JSON: {"templateId":"...","difficulty":1-3,"durationSeconds":120-300,"briefing":"<=160 chars, second person",
"objectives":[{"id":"obj_1","type":"...","targetId":"","steps":3}],"npcBeatIds":["..."]}
Templates: ${TEMPLATES.join(", ")}. Objective types: ${OBJECTIVES.join(", ")}.
If requestedTemplate is set, use it: TREASURE_HUNT = 2-3 HIDDEN_RELIC then SAFE_DIAL (steps = relic count);
GREENHOUSE = NUTRIENT_MIX (steps 3) then GROW_LIGHT (steps 3); GENE_LAB = MICROSCOPE_FOCUS (steps 2-4) then DNA_SPLICE (steps 3-5).
Otherwise 2-4 objectives; every ENTER_CODE has targetId = the id of a FIND_CLUE before it.
difficulty = recentDifficulty. lastChoice LOUD_ROUTE -> include DISABLE_SENSOR. npcBeatIds = a few of npcBeats.`,
  puzzle_architect: `You place puzzles on real surfaces. Return ONLY {"placements":[{"objectiveId":"...","anchorId":"..."}]}
with one placement per objective. anchorId MUST be one of the ids in input.surfaces (closest first). Spread objectives
over different surfaces when there are several. Never invent ids.`,
  npc: `Write one short spoken line for ROOM//ZERO. Return ONLY {"intent":"...","text":"..."}.
intent: one of IDLE, INTRO, SUSPICIOUS, ALERT, CALM, PRAISE, TAUNT, HINT. text: max 90 characters, family-friendly.
beatId starting zero_ = ZERO, a dry confident heist handler. guard_ = a patrol drone, clipped and robotic.`,
};

PROMPTS.director_fixed = `You are the Game Director of ROOM//ZERO, a family-friendly mixed-reality heist in the player's real room.
The room layout is fixed by requestedTemplate. Return ONLY JSON:
{"difficulty":1-3,"durationSeconds":120-300,"briefing":"<=140 chars, second person, mention the room's goal","stepCounts":[a,b]}
stepCounts: TREASURE_HUNT -> [number of relics 2-3]; GREENHOUSE -> [nutrient steps 3, light steps 3];
GENE_LAB -> [microscope steps 2-4, splice steps 3-5]. Higher difficulty -> more steps, less time.
difficulty = recentDifficulty. If lastChoice is LOUD_ROUTE, the briefing teases that security is tighter now;
if SILENT_ROUTE, that they moved the vault.`;

const MAX_TOKENS = { director_fixed: 110, director: 260, puzzle_architect: 160, npc: 60, adaptation: 60, mentor: 50 };

// ---------------------------------------------------------------- repair: keep the model's decision, fix its format
const up = v => String(v ?? "").trim().toUpperCase().replace(/[\s-]+/g, "_");
const clamp = (v, a, b, d) => { const n = Number(v); return Number.isFinite(n) ? Math.min(b, Math.max(a, Math.round(n))) : d; };
const cut = (t, n) => { t = String(t ?? "").replace(/[\u0000-\u001f]/g, " ").replace(/^["']|["']$/g, "").trim(); if (t.length <= n) return t; const c = t.slice(0, n - 1); return c.slice(0, Math.max(c.lastIndexOf(" "), n - 20)) + "…"; };
const pick = (v, list, d) => { const u = up(v); return list.includes(u) ? u : (list.find(x => u.includes(x)) || d); };

function repair(agent, input, out, fixes) {
  const note = m => fixes.push(m);
  if (!out || typeof out !== "object") throw new Error("not a JSON object");
  switch (agent) {
    case "mentor": {
      const allowed = input?.allowedActions?.length ? input.allowedActions : ["WAIT", "ENCOURAGE", "HINT", "SHOW_ONE_STEP"];
      let a = pick(out.action, allowed, null);
      if (!a) throw new Error(`unknown mentor action ${out.action}`);
      if (a !== up(out.action)) note("action");
      if (a === "SHOW_ONE_STEP" && (input?.secondsStuck ?? 0) < 20) { a = "HINT"; note("too early to show a step"); }
      return { action: a, reason: cut(out.reason || "model decision", 60) };
    }
    case "npc": {
      const intents = ["IDLE", "INTRO", "SUSPICIOUS", "ALERT", "CALM", "PRAISE", "TAUNT", "HINT"];
      const text = cut(out.text || out.line || "", 110);
      if (!text) throw new Error("empty line");
      const intent = pick(out.intent, intents, input?.beatId?.startsWith("guard_") ? "SUSPICIOUS" : "INTRO");
      if (intent !== up(out.intent)) note("intent");
      return { beatId: input?.beatId, intent, text };
    }
    case "adaptation": {
      const acts = ["NONE", "OFFER_HINT", "EXTEND_TIMER", "LOWER_GUARD_ALERTNESS", "RAISE_GUARD_ALERTNESS"];
      let a = pick(out.action, acts, "NONE");
      if ((input?.alreadyApplied || []).includes(a)) { a = "NONE"; note("already applied"); }
      const amount = a === "EXTEND_TIMER" ? clamp(out.amount, 10, 60, 30) : a.includes("GUARD") ? clamp(out.amount, 5, 40, 15) : 0;
      return { action: a, amount, reason: cut(out.reason || "model decision", 80) };
    }
    case "puzzle_architect": {
      const surf = validSurfaces(input).map(s => s.id);
      if (!surf.length) return { placements: [] };
      const objs = (input?.plan?.objectives || []).map(o => o.id);
      const given = new Map((out.placements || []).filter(p => p && surf.includes(p.anchorId)).map(p => [p.objectiveId, p.anchorId]));
      return { placements: objs.map((id, i) => {
        if (given.has(id)) return { objectiveId: id, anchorId: given.get(id) };
        note(`placed ${id}`); return { objectiveId: id, anchorId: surf[i % surf.length] };
      }) };
    }
    case "director": {
      const req = up(input?.requestedTemplate);
      if (Array.isArray(out.stepCounts) && !Array.isArray(out.objectives)) {
        const c = out.stepCounts;
        out.objectives = req === "TREASURE_HUNT"
          ? [...Array(clamp(c[0], 2, 3, 2))].map(() => ({ type: "HIDDEN_RELIC", steps: 1 })).concat([{ type: "SAFE_DIAL" }])
          : req === "GREENHOUSE" ? [{ type: "NUTRIENT_MIX", steps: c[0] }, { type: "GROW_LIGHT", steps: c[1] }]
          : req === "GENE_LAB" ? [{ type: "MICROSCOPE_FOCUS", steps: c[0] }, { type: "DNA_SPLICE", steps: c[1] }] : [];
      }
      const templateId = TEMPLATES.includes(req) ? req : pick(out.templateId, TEMPLATES, "SERVER_HEIST");
      let objs = (Array.isArray(out.objectives) ? out.objectives : [])
        .map((o, i) => ({ id: "obj_" + (i + 1), type: pick(o?.type, OBJECTIVES, null), targetId: "", anchorId: "", steps: clamp(o?.steps, 0, 5, 3), _t: o?.targetId, _id: o?.id }))
        .filter(o => o.type);
      const shape = { TREASURE_HUNT: null, GREENHOUSE: ["NUTRIENT_MIX", "GROW_LIGHT"], GENE_LAB: ["MICROSCOPE_FOCUS", "DNA_SPLICE"] }[templateId];
      if (templateId === "TREASURE_HUNT") {
        const n = Math.min(3, Math.max(2, objs.filter(o => o.type === "HIDDEN_RELIC").length || 2));
        if (objs.length !== n + 1 || objs.slice(0, n).some(o => o.type !== "HIDDEN_RELIC") || objs[n].type !== "SAFE_DIAL") note("treasure layout");
        objs = [...Array(n)].map((_, i) => ({ id: "obj_" + (i + 1), type: "HIDDEN_RELIC", targetId: "", anchorId: "", steps: 1 }))
          .concat([{ id: "obj_" + (n + 1), type: "SAFE_DIAL", targetId: "", anchorId: "", steps: n }]);
      } else if (shape) {
        const steps = shape.map(t => objs.find(o => o.type === t)?.steps);
        if (objs.length !== 2 || objs[0].type !== shape[0] || objs[1].type !== shape[1]) note("lab layout");
        const lim = { NUTRIENT_MIX: [3, 3], GROW_LIGHT: [3, 3], MICROSCOPE_FOCUS: [2, 4], DNA_SPLICE: [3, 5] };
        objs = shape.map((t, i) => ({ id: "obj_" + (i + 1), type: t, targetId: "", anchorId: "", steps: clamp(steps[i], lim[t][0], lim[t][1], lim[t][0]) }));
      } else {
        objs = objs.filter(o => !["HIDDEN_RELIC", "SAFE_DIAL", "NUTRIENT_MIX", "GROW_LIGHT", "MICROSCOPE_FOCUS", "DNA_SPLICE"].includes(o.type)).slice(0, 4);
        if (!objs.length) throw new Error("no usable objectives");
        // ENTER_CODE needs a FIND_CLUE before it.
        objs.forEach((o, i) => {
          if (o.type !== "ENTER_CODE") return;
          const clue = objs.slice(0, i).find(c => c.type === "FIND_CLUE");
          if (clue) o.targetId = clue.id;
          else { objs.splice(i, 0, { id: "", type: "FIND_CLUE", targetId: "", anchorId: "", steps: 0 }); note("added clue"); }
        });
        objs.forEach((o, i) => { const old = o.id; o.id = "obj_" + (i + 1); objs.forEach(x => { if (x.targetId === old && old) x.targetId = o.id; }); });
        objs.forEach(o => { if (o.type === "ENTER_CODE") { const c = objs.find(x => x.type === "FIND_CLUE"); o.targetId = c ? c.id : ""; o.steps = clamp(o.steps, 4, 4, 4); } if (o.type === "FIND_CLUE") o.steps = 0; });
      }
      objs.forEach(o => { delete o._t; delete o._id; });
      const beats = input?.npcBeats || [];
      const npc = (Array.isArray(out.npcBeatIds) ? out.npcBeatIds : []).filter(b => beats.includes(b));
      return {
        schemaVersion: 1, missionId: "heist_ai_" + Date.now().toString(36), templateId,
        difficulty: clamp(out.difficulty, 1, 3, input?.profile?.stats?.recentDifficulty || 1),
        durationSeconds: clamp(out.durationSeconds, 120, 300, 210),
        briefing: cut(out.briefing || "ZERO has a job for you. Move fast, stay quiet.", 200),
        objectives: objs,
        npcBeatIds: npc.length ? npc : beats.slice(0, 7),
        successCondition: "ALL_OBJECTIVES_COMPLETE",
      };
    }
  }
  return out;
}

// ---------------------------------------------------------------- llm call
const FIXED = ["TREASURE_HUNT", "GREENHOUSE", "GENE_LAB"];
const variant = (agent, input) => agent === "director" && FIXED.includes(up(input?.requestedTemplate)) ? "director_fixed" : agent;
async function llm(agent, input, signal) {
  const res = await fetch(LLM_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(LLM_KEY ? { Authorization: `Bearer ${LLM_KEY}` } : {}) },
    body: JSON.stringify({
      model: LLM_MODEL,
      temperature: agent === "npc" ? 0.7 : 0.2,
      max_tokens: MAX_TOKENS[variant(agent, input)] || 200,
      response_format: { type: "json_object" },
      keep_alive: "30m",
      messages: [{ role: "system", content: PROMPTS[variant(agent, input)] }, { role: "user", content: JSON.stringify(compact(agent, input)) }],
    }),
    signal,
  });
  if (!res.ok) throw new Error(`LLM HTTP ${res.status}`);
  const data = await res.json();
  let text = data.choices?.[0]?.message?.content || "";
  const m = text.match(/\{[\s\S]*\}/);           // tolerate ```json fences or chatter around the object
  if (!m) throw new Error("no JSON in reply");
  return JSON.parse(m[0]);
}


// ---------------------------------------------------------------- scheduler
// A local model answers one request at a time. Without a queue, slow NPC lines block Dr. Asha's
// mentor and the adaptation agent, and everything times out together. So: one model call at a time,
// most urgent first, and every request has a deadline that matches what the game is willing to wait.
const PRIORITY = { mentor: 0, adaptation: 1, director: 1, puzzle_architect: 1, npc: 2, background_plan: 3, background: 4 };
const BUDGET_MS = {   // total time incl. waiting in the queue; keep below the game's timeouts
  mentor: Number(process.env.BUDGET_MENTOR_MS || 8000),
  adaptation: Number(process.env.BUDGET_ADAPT_MS || 8000),
  npc: Number(process.env.BUDGET_NPC_MS || 9000),
  director: Number(process.env.BUDGET_PLAN_MS || 14000),
  puzzle_architect: Number(process.env.BUDGET_PLAN_MS || 14000),
  background: 30000,
  background_plan: 45000,
};
const CONCURRENCY = Number(process.env.LLM_CONCURRENCY || 1);
const queue = [];

const isBackground = k => k.startsWith("background");
const runningJobs = new Set();

function schedule(kind, agent, input) {
  return new Promise((resolve, reject) => {
    const job = { kind, agent, input, resolve, reject, prio: PRIORITY[kind] ?? 2, deadline: Date.now() + (BUDGET_MS[kind] || 10000), seq: queue.seq = (queue.seq || 0) + 1, preempted: 0 };
    enqueue(job);
    // A player is waiting: interrupt background pre-writing instead of queueing behind it.
    if (!isBackground(kind) && runningJobs.size >= CONCURRENCY)
      for (const r of runningJobs) if (isBackground(r.kind)) { r.controller.abort("preempt"); break; }
    pump();
  });
}
function enqueue(job) { queue.push(job); queue.sort((a, b) => a.prio - b.prio || a.seq - b.seq); }
function pump() {
  while (runningJobs.size < CONCURRENCY && queue.length) {
    const job = queue.shift();
    const left = job.deadline - Date.now();
    if (left < 800) { job.reject(new Error(`gave up: waited ${BUDGET_MS[job.kind] - left}ms in queue`)); continue; }
    job.controller = new AbortController();
    const timer = setTimeout(() => job.controller.abort("timeout"), left);
    runningJobs.add(job);
    llm(job.agent, job.input, job.controller.signal)
      .then(job.resolve, e => {
        const why = job.controller.signal.reason;
        if (why === "preempt" && job.preempted < 5) { job.preempted++; job.deadline = Date.now() + BUDGET_MS[job.kind]; enqueue(job); return; }
        job.reject(why === "timeout" ? new Error(`model slower than ${BUDGET_MS[job.kind]}ms`) : e);
      })
      .finally(() => { clearTimeout(timer); runningJobs.delete(job); pump(); });
  }
}

// ---------------------------------------------------------------- NPC line cache
// ZERO's and the drone's lines are written by the model ahead of time, in the background, so the
// game gets a fresh AI-written line instantly instead of waiting for the model mid-play.
const BEATS = ["zero_intro", "zero_treasure_intro", "zero_greenhouse_intro", "zero_genelab_intro", "zero_return_silent", "zero_return_loud",
  "zero_hint", "zero_praise", "zero_fail", "guard_intro", "guard_suspicious", "guard_alert", "guard_calm", "guard_react_silent", "guard_react_loud"];
const npcCache = new Map();            // beatId -> [line, ...]
const refilling = new Set();
const CACHE_PER_BEAT = 2;

function refill(beatId, ctx = {}) {
  if (MODE !== "llm" || refilling.has(beatId)) return;
  if ((npcCache.get(beatId) || []).length >= CACHE_PER_BEAT) return;
  refilling.add(beatId);
  const input = { beatId, missionState: ctx.missionState || "ACTIVE", route: ctx.route || "NONE", guardAlertness: ctx.guardAlertness || 0 };
  schedule("background", "npc", input)
    .then(raw => { const line = repair("npc", input, raw, []); const list = npcCache.get(beatId) || []; if (!list.some(l => l.text === line.text)) list.push(line); npcCache.set(beatId, list); })
    .catch(() => {})
    .finally(() => { refilling.delete(beatId); if ((npcCache.get(beatId) || []).length < CACHE_PER_BEAT) setTimeout(() => refill(beatId, ctx), 3000); });
}
function warmNpcCache() { if (MODE === "llm") BEATS.forEach(b => refill(b)); }

// ---------------------------------------------------------------- Director plan cache
// Writing a whole mission plan can take a small local model 10-20 s. So the model plans each room in
// advance, for each way the player's last run could have gone, and the game gets that plan instantly.
const PLAN_TEMPLATES = ["TREASURE_HUNT", "GREENHOUSE", "GENE_LAB", ""];
const CHOICES = ["NONE", "SILENT_ROUTE", "LOUD_ROUTE"];
const planCache = new Map();          // "TEMPLATE|CHOICE" -> raw model plan
const planKey = input => `${up(input?.requestedTemplate)}|${input?.profile?.lastSession?.choice || "NONE"}`;
function planInput(t, choice, base) {
  return { requestedTemplate: t, npcBeats: base?.npcBeats || BEATS,
    profile: { stats: { recentDifficulty: base?.profile?.stats?.recentDifficulty || 1 }, lastSession: { choice } } };
}
function refillPlan(input) {
  const key = planKey(input);
  if (MODE !== "llm" || refilling.has("plan:" + key) || planCache.has(key)) return;
  refilling.add("plan:" + key);
  schedule("background_plan", "director", input)
    .then(raw => { repair("director", input, JSON.parse(JSON.stringify(raw)), []); planCache.set(key, raw); })
    .catch(() => {})
    .finally(() => refilling.delete("plan:" + key));
}
function warmPlans() { if (MODE !== "llm") return; for (const c of CHOICES) for (const t of PLAN_TEMPLATES) refillPlan(planInput(t, c)); }

async function answer(agent, input) {
  if (MODE !== "llm") return { raw: mock(agent, input), via: "mock" };
  if (agent === "director") {
    const key = planKey(input), raw = planCache.get(key);
    if (raw) {
      planCache.delete(key); refillPlan(input);                     // keep one ready for next time
      return { raw: { ...raw, difficulty: input?.profile?.stats?.recentDifficulty || raw.difficulty }, via: "cache" };
    }
    refillPlan(input);
  }
  if (agent === "npc") {
    const list = npcCache.get(input?.beatId) || [];
    if (list.length) { const line = list.shift(); refill(input.beatId, input); return { raw: line, via: "cache" }; }
    refill(input?.beatId, input);
  }
  return { raw: await schedule(agent, agent, input), via: "live" };
}

export { compact, repair, validSurfaces, PROMPTS };

// ---------------------------------------------------------------- server
if (process.env.NO_SERVER) { /* imported by test-agents.mjs */ } else http.createServer(async (req, res) => {
  const m = req.url.match(/^\/agents\/(director|puzzle_architect|npc|adaptation|mentor)$/);
  if (req.method !== "POST" || !m) { res.writeHead(404).end(); return; }
  const agent = m[1];
  let body = "";
  for await (const chunk of req) { body += chunk; if (body.length > 200_000) { res.writeHead(413).end(); return; } }
  const t0 = Date.now();
  try {
    const input = JSON.parse(body || "{}");
    const fixes = [];
    const { raw, via } = await answer(agent, input);
    const out = repair(agent, input, raw, fixes);
    if (via === "cache") fixes.unshift("pre-written by the model");
    if (!out) throw new Error("no output");
    res.writeHead(200, { "Content-Type": "application/json" }).end(JSON.stringify(out));
    console.log(`[proxy] ${agent} ok ${Date.now() - t0}ms${fixes.length ? "  (" + fixes.join(", ") + ")" : ""}`);
  } catch (e) {
    console.warn(`[proxy] ${agent} failed ${Date.now() - t0}ms: ${e.message}`);
    res.writeHead(502, { "Content-Type": "application/json" }).end(JSON.stringify({ error: e.message }));
  }
}).listen(PORT, "0.0.0.0", () => {
  console.log(`ROOM//ZERO agent proxy on :${PORT} (MODE=${MODE})`);
  if (MODE === "llm") { console.log("[proxy] pre-writing mission plans and ZERO's lines in the background (2-4 min)..."); warmPlans(); warmNpcCache(); }
});
