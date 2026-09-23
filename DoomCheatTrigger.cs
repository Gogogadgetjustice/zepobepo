using System;
using System.Text;
using UnityEngine;

namespace BigWalkDoom
{
    /// <summary>
    /// Sits near the couch/credits screen. Listens for "iddqd", proximity, or 
    /// shortcut keys ('\' to open Doom, '+' to teleport to screen).
    ///
    /// Also doubles as a live screen-fitting rig: while Doom is open, [ ] resize
    /// it, arrow keys nudge it left/right/up/down along the screen's own local
    /// axes, and Page Up/Down push it forward/back in depth. Every adjustment
    /// gets logged so you can copy the final numbers into OverlayScale /
    /// OverlayLocalOffset once it looks right, instead of guessing blind.
    /// </summary>
    public class DoomCheatTrigger : MonoBehaviour
    {
        // Required boilerplate for any custom type registered with IL2CPP.
        public DoomCheatTrigger(IntPtr ptr) : base(ptr) { }

        private const string CheatCode = "iddqd";
        private const float ProximityRadius = 16.0f;
        private static readonly Vector3 TeleportTarget = new Vector3(-912.40f, 7.27f, 770.21f);

        private StringBuilder buffer = new StringBuilder();
        private DoomOverlay overlay;
        private GameObject overlayWindowObject;
        private Transform playerTransform;

        // Exact hierarchy path from scene dump. NOTE: this is very likely an
        // empty "Positioner" marker object near the base of the actual screen
        // prop, not the screen mesh itself - it resolves to *something*, but
        // at native (1,1,1) scale, unrelated to how big Big Walk's screen
        // actually is. That's why the overlay spawns tiny. Hence the live
        // resize/reposition controls below instead of trusting this anchor's
        // own scale.
        private const string PreferredAnchorPath = "LandmarksNonChallenge/CreditsTheatre/Positioner/CreditsScreen";

        // Four corners fallback
        private static readonly Vector3 ScreenCornerTopLeft = new Vector3(-914.79f, 11.21f, 766.00f);
        private static readonly Vector3 ScreenCornerTopRight = new Vector3(-917.59f, 11.26f, 770.22f);
        private static readonly Vector3 ScreenCornerBottomRight = new Vector3(-918.12f, 8.02f, 769.99f);
        private static readonly Vector3 ScreenCornerBottomLeft = new Vector3(-915.12f, 7.77f, 765.42f);

        // ---- Live-tunable screen fit ----
        // Starting guesses only - nudge these live with [ ] / arrows / PageUp
        // /PageDown once Doom is open, then bake the final numbers in here.
        private static float OverlayScale = 4.5f;
        private static Vector3 OverlayLocalOffset = new Vector3(0f, 3f, 0.50f);
        private const float ScaleStep = 0.5f;
        private const float MoveStep = 0.25f;

        public static void SpawnAtScreen()
        {
            var anchor = GameObject.Find(PreferredAnchorPath) ?? GameObject.Find("CreditsScreen");
            GameObject go = new GameObject("DoomCheatTrigger");

            if (anchor != null)
            {
                go.transform.SetParent(anchor.transform, false);
                Plugin.InstanceLog.LogInfo($"DoomCheatTrigger: attached directly to '{anchor.name}'.");
            }
            else
            {
                var center = (ScreenCornerTopLeft + ScreenCornerTopRight + ScreenCornerBottomRight + ScreenCornerBottomLeft) / 2f;
                var right = (ScreenCornerTopRight - ScreenCornerTopLeft).normalized;
                var down = (ScreenCornerBottomLeft - ScreenCornerTopLeft).normalized;
                var normal = Vector3.Cross(down, right).normalized;

                go.transform.position = center;
                go.transform.rotation = Quaternion.LookRotation(normal, -down);

                Plugin.InstanceLog.LogWarning("DoomCheatTrigger: Anchor not found, using hardcoded position.");
            }

            go.AddComponent<DoomCheatTrigger>();
        }

        private void Update()
        {
            // Locate local player dynamically if transform isn't cached
            if (playerTransform == null)
            {
                LocatePlayer();
            }

            // Global Hotkey: Press '\' to instantly launch or toggle Doom.
            // (Deliberately not F1 - Doom's own engine binds F1 to its
            // internal help screen once it has input focus, so our toggle
            // and Doom's own menu would fight over the same key.)
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                ToggleDoom();
            }

            // Global Hotkey: Teleport to screen position when pressing '+'
            if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
            {
                TeleportPlayer();
            }

            if (overlayWindowObject != null)
            {
                HandleFitControls();
            }

            if (playerTransform == null) return;

            bool inRange = Vector3.Distance(transform.position, playerTransform.position) <= ProximityRadius;

            if (!inRange)
            {
                buffer.Clear();
                return;
            }

            CaptureTypedCharacters();

            if (buffer.ToString().EndsWith(CheatCode, StringComparison.OrdinalIgnoreCase))
            {
                buffer.Clear();
                ToggleDoom();
            }
        }

        // Resize with [ / ], nudge along the screen's own right/up with the
        // arrow keys, push depth in/out with Page Up/Page Down. Logs the
        // running scale + local offset after every change.
        private void HandleFitControls()
        {
            bool changed = false;

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                OverlayScale += ScaleStep;
                changed = true;
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                OverlayScale = Mathf.Max(0.1f, OverlayScale - ScaleStep);
                changed = true;
            }

//            if (Input.GetKeyDown(KeyCode.RightArrow))
//            {
//                OverlayLocalOffset += new Vector3(MoveStep, 0f, 0f);
//                changed = true;
//            }
//            else if (Input.GetKeyDown(KeyCode.L))
//            {
//                OverlayLocalOffset += new Vector3(-MoveStep, 0f, 0f);
//                changed = true;
//            }

 //           if (Input.GetKeyDown(KeyCode.UpArrow))
 //           {
 //               OverlayLocalOffset += new Vector3(0f, MoveStep, 0f);
  //              changed = true;
  //          }
  //          else if (Input.GetKeyDown(KeyCode.DownArrow))
  //          {
  //              OverlayLocalOffset += new Vector3(0f, -MoveStep, 0f);
   //             changed = true;
    //        }

//            if (Input.GetKeyDown(KeyCode.PageUp))
//            {
//                OverlayLocalOffset += new Vector3(0f, 0f, MoveStep);
//                changed = true;
//            }
            else if (Input.GetKeyDown(KeyCode.PageDown))
            {
                OverlayLocalOffset += new Vector3(0f, 0f, -MoveStep);
                changed = true;
            }

            if (!changed) return;

            ApplyFit();
            Plugin.InstanceLog.LogInfo(
                $"Overlay fit: scale={OverlayScale:F2}, offset=({OverlayLocalOffset.x:F2}, {OverlayLocalOffset.y:F2}, {OverlayLocalOffset.z:F2})");
        }

        private void ApplyFit()
        {
            if (overlayWindowObject == null) return;
            overlayWindowObject.transform.localPosition = OverlayLocalOffset;
            overlayWindowObject.transform.localScale = Vector3.one * OverlayScale;
        }

        private void LocatePlayer()
        {
            var playerObj = GameObject.FindWithTag("Player");

            if (playerObj == null && Camera.main != null)
            {
                playerObj = Camera.main.transform.root.gameObject;
            }

            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        private void TeleportPlayer()
        {
            if (playerTransform == null)
            {
                LocatePlayer();
                if (playerTransform == null)
                {
                    Plugin.InstanceLog.LogWarning("Teleport failed: Player transform not found.");
                    return;
                }
            }

            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = TeleportTarget;

            if (cc != null) cc.enabled = true;

            var rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Plugin.InstanceLog.LogInfo($"Teleported player to {TeleportTarget}");
        }

        private void CaptureTypedCharacters()
        {
            string typed = Input.inputString;
            if (string.IsNullOrEmpty(typed)) return;

            foreach (char c in typed)
            {
                if (char.IsLetterOrDigit(c)) buffer.Append(char.ToLowerInvariant(c));
                if (buffer.Length > CheatCode.Length) buffer.Remove(0, buffer.Length - CheatCode.Length);
            }
        }

        private void ToggleDoom()
        {
            if (overlay == null)
            {
                overlayWindowObject = new GameObject("DoomOverlayWindow");
                overlayWindowObject.transform.SetParent(transform, false);
                ApplyFit();

                overlay = overlayWindowObject.AddComponent<DoomOverlay>();
                overlay.Open();
                Plugin.InstanceLog.LogInfo(
                    $"Doom toggled on via '\\' / iddqd. Overlay fit: scale={OverlayScale:F2}, offset=({OverlayLocalOffset.x:F2}, {OverlayLocalOffset.y:F2}, {OverlayLocalOffset.z:F2}). " +
                    "Use [ ] to resize, arrow keys to nudge, PageUp/PageDown for depth.");
            }
            else
            {
                overlay.Close();
            }
        }

        private void OnDestroy()
        {
            if (overlay != null) overlay.Shutdown();
        }
    }
}