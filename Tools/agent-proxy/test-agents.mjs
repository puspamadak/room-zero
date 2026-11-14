// Measures how often each agent's answer comes from the AI model, and how fast.
// Run while the proxy is running:   node test-agents.mjs        (5 rounds)   node test-agents.mjs 10
const URL = process.env.PROXY || "http://127.0.0.1:8787";
const ROUNDS = Number(process.argv[2] || 5);
const LIMIT = { director: 15000, puzzle_architect: 15000, npc: 10000, adaptation: 10000, mentor: 10000 };   // what the game waits

const room = {
  source: "MOCK", playerOrigin: [0, 0, 0],
  surfaces: [
    { id: "surface-desk", type: "table", center: [0, 0.75, 0.5], size: [1.4, 0.03, 0.7], normal: [0, 1, 0], allowed: true, confidence: 1 },
    { id: "surface-side", type: "table", center: [0.6, 0.7, 0.2], size: [0.5, 0.03, 0.5], normal: [0, 1, 0], allowed: true, confidence: 0.9 },
    { id: "surface-wall-front", type: "wall", center: [0, 1.3, 1.9], size: [3, 2.6, 0.02], normal: [0, 0, -1], allowed: true, confidence: 0.9 },
  ],
};
const beats = ["zero_intro", "zero_hint", "zero_praise", "zero_fail", "guard_intro", "guard_suspicious", "guard_alert"];
const profile = { stats: { recentDifficulty: 2 }, lastSession: { choice: "LOUD_ROUTE" } };
const cases = [
  ["director", { room, profile, requestedTemplate: "", npcBeats: beats }],
  ["director", { room, profile, requestedTemplate: "GREENHOUSE", npcBeats: beats }],
  ["puzzle_architect", { room, reachMeters: 0.75, plan: { objectives: [{ id: "obj_1", type: "NUTRIENT_MIX" }, { id: "obj_2", type: "GROW_LIGHT" }] } }],
  ["npc", { beatId: "guard_suspicious", missionState: "ACTIVE", route: "LOUD", guardAlertness: 0.6 }],
  ["adaptation", { signals: { elapsedSeconds: 90, durationSeconds: 210, failedAttempts: 3, hintsUsed: 0, objectivesComplete: 0, objectivesTotal: 2 }, alreadyApplied: [] }],
  ["mentor", { objectiveType: "GROW_LIGHT", secondsStuck: 25, failedAttempts: 2, hintsGiven: 1, allowedActions: ["WAIT", "ENCOURAGE", "HINT", "SHOW_ONE_STEP"] }],
];

const stats = {};
for (let r = 1; r <= ROUNDS; r++) {
  for (const [agent, input] of cases) {
    const s = (stats[agent] ??= { ai: 0, slow: 0, fail: 0, ms: [] });
    const t0 = Date.now();
    try {
      const res = await fetch(`${URL}/agents/${agent}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(input) });
      const ms = Date.now() - t0; s.ms.push(ms);
      if (!res.ok) { s.fail++; console.log(`  ✘ ${agent} ${ms}ms ${(await res.text()).slice(0, 80)}`); continue; }
      const out = await res.json();
      if (ms > LIMIT[agent]) { s.slow++; console.log(`  ⏱ ${agent} ${ms}ms (game would fall back)`); }
      else { s.ai++; console.log(`  ✔ ${agent} ${ms}ms ${JSON.stringify(out).slice(0, 90)}`); }
    } catch (e) { s.fail++; console.log(`  ✘ ${agent}: ${e.message} (is the proxy running?)`); }
  }
}
console.log("\n== AI share per agent ==");
let ai = 0, all = 0;
for (const [a, s] of Object.entries(stats)) {
  const n = s.ai + s.slow + s.fail; ai += s.ai; all += n;
  const avg = s.ms.length ? Math.round(s.ms.reduce((x, y) => x + y, 0) / s.ms.length) : 0;
  console.log(`${a.padEnd(17)} ${String(Math.round(100 * s.ai / n)).padStart(3)}% AI   avg ${avg}ms   slow ${s.slow}  failed ${s.fail}`);
}
console.log(`TOTAL             ${Math.round(100 * ai / all)}% AI`);
