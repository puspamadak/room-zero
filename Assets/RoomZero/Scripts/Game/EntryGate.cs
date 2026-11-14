// Biometric palm scanner at the start of Room 1: "Place your hand to enter".
// Hands-first: touching the glass pad with a hand (or a click on desktop) starts the scan.
// Verified -> the lab doors open (desktop), the lights come on, and the mission continues.
// Owner: #3 Interaction.

using System;
using System.Collections;
using UnityEngine;
using RoomZero.Input;
using RoomZero.Spatial;
using RoomZero.UI;

namespace RoomZero.Game
{
    public class EntryGate : MonoBehaviour
    {
        public static EntryGate Instance { get; private set; }

        Action onVerified;
        TextMesh screen, sub;
        Transform scanLine, pad;
        Renderer ring;
        bool scanning, done;
        public bool Verified => done;
        float shownAt;

        public static EntryGate Show(RoomMap room, Action onVerified)
        {
            Close();
            var desk = room.FindSurface("surface-desk");
            Vector3 origin = room.PlayerOrigin;
            Vector3 at, toPlayer;
            if (desk != null)
            {
                toPlayer = origin - desk.Center; toPlayer.y = 0; toPlayer.Normalize();
                at = desk.Center + toPlayer * Mathf.Min(desk.Size.x, desk.Size.z) * 0.2f + Vector3.up * 0.01f;
            }
            else
            {
                toPlayer = Vector3.back;
                at = origin + Vector3.forward * 0.45f + Vector3.down * 0.4f;
            }
            var go = new GameObject("BiometricScanner");
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);
            var g = go.AddComponent<EntryGate>();
            g.onVerified = onVerified;
            g.Build();
            Instance = g;
            if (RoomDebugView.Instance) RoomDebugView.Instance.ShowEntrance();
            return g;
        }

        public static void Close()
        {
            if (Instance) Destroy(Instance.gameObject);
            Instance = null;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            shownAt = Time.time;
            // Pedestal: brushed steel housing with a dark glass palm pad tilted towards the player.
            var housing = Shapes.RoundCube("Housing", transform, new Vector3(0, 0.03f, 0.02f), new Vector3(0.26f, 0.06f, 0.2f), Color.white, 0.015f);
            housing.GetComponent<Renderer>().sharedMaterial = Shapes.Steel;
            Shapes.ContactShadow(transform, transform.position, 0.34f, 0.28f, transform.rotation, 0.45f);

            pad = new GameObject("PadPivot").transform;
            pad.SetParent(transform, false);
            pad.localPosition = new Vector3(0, 0.065f, 0.0f);
            pad.localRotation = Quaternion.Euler(-28f, 0, 0);   // tilt the glass up towards the player's hand
            var bezel = Shapes.RoundCube("Bezel", pad, new Vector3(0, 0, 0), new Vector3(0.22f, 0.012f, 0.17f), Color.white, 0.006f);
            bezel.GetComponent<Renderer>().sharedMaterial = Shapes.BlackPlastic;
            var glass = Shapes.RoundCube("PalmPad", pad, new Vector3(0, 0.0065f, 0), new Vector3(0.15f, 0.002f, 0.2f), Color.white, 0.001f, lit: false);
            var padMat = new Material(UiFactory.Mat(Color.white)) { mainTexture = LabTextures.HandPrint };
            glass.GetComponent<Renderer>().sharedMaterial = padMat;
            // The hand image is drawn in the pad's own plane; turn it so the fingers point away from the player.
            glass.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var col = glass.AddComponent<BoxCollider>();
            col.size = new Vector3(0.15f, 0.06f, 0.2f);
            var poke = glass.AddComponent<PokeTarget>();
            poke.targetId = "entry_palm";
            poke.Pressed += _ => StartScan();

            var ringGo = Shapes.RoundCube("Ring", pad, new Vector3(0, 0.0072f, 0), new Vector3(0.214f, 0.0008f, 0.164f), Palette.Holo, 0.002f, lit: false);
            ring = ringGo.GetComponent<Renderer>();
            scanLine = Shapes.RoundCube("ScanLine", pad, new Vector3(0, 0.009f, -0.07f), new Vector3(0.2f, 0.002f, 0.006f), new Color(0.5f, 1f, 1f), 0.001f, lit: false).transform;
            scanLine.gameObject.SetActive(false);

            screen = UiFactory.BackedText(transform, "PLACE YOUR HAND TO ENTER", new Vector3(0, 0.2f, 0.06f), 0.02f, Palette.Holo, faceCamera: false);
            sub = UiFactory.Text(transform, "BIO-SECURE LAB  ·  PALM ID", new Vector3(0, 0.168f, 0.055f), 0.012f, Palette.Muted);
            gameObject.AddComponent<Sparkles>().color = Palette.Holo;
        }

        void Update()
        {
            if (scanning || done) return;
            // Gentle pulse on the pad ring; after a while, nudge harder.
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * (Time.time - shownAt > 12f ? 7f : 3f));
            ring.sharedMaterial = UiFactory.Mat(Color.Lerp(Palette.Button, Palette.Holo, k));
        }

        public void StartScan()
        {
            if (scanning || done || PokeTarget.InputBlocked) return;
            StartCoroutine(Scan());
        }

        IEnumerator Scan()
        {
            scanning = true;
            screen.text = "SCANNING...";
            screen.color = Palette.Accent;
            scanLine.gameObject.SetActive(true);
            RoomZero.Audio.Sfx.Play("scan");
            for (int pass = 0; pass < 2; pass++)
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.7f)
                {
                    float z = Mathf.Lerp(-0.07f, 0.07f, pass == 0 ? t : 1f - t);
                    scanLine.localPosition = new Vector3(0, 0.009f, z);
                    yield return null;
                }
            scanLine.gameObject.SetActive(false);
            ring.sharedMaterial = UiFactory.Mat(Palette.Success);
            screen.text = "IDENTITY VERIFIED";
            screen.color = Palette.Success;
            sub.text = "WELCOME, AGENT. DOORS OPENING";
            sub.color = Palette.Success;
            RoomZero.Audio.Sfx.Play("success");
            Fx.Burst(pad.position, Palette.Success, 0.2f);
            done = true;
            if (RoomDebugView.Instance) RoomDebugView.Instance.OpenEntrance();
            yield return new WaitForSeconds(1.2f);
            var cb = onVerified;
            onVerified = null;
            cb?.Invoke();
            yield return new WaitForSeconds(1.0f);
            Destroy(gameObject);
        }
    }
}
