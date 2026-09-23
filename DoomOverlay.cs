using System;
using ManagedDoom;
using ManagedDoom.Unity;
using UnityEngine;

namespace BigWalkDoom
{
    /// <summary>
    /// Owns one running instance of the Doom engine and drives it every frame.
    /// This is the direct replacement for the OW mod's Doom.cs + DoomShipLogMode.cs,
    /// minus everything that was specific to the ship log menu.
    /// </summary>
    public class DoomOverlay : MonoBehaviour
    {
        public DoomOverlay(IntPtr ptr) : base(ptr) { }

        private UnityDoom doomGame;
        private bool running;

        public void Open()
        {
            if (doomGame != null)
            {
                // Don't touch Volume here (may access a null music instance).
                doomGame.Visible = true;
                doomGame.AllowInput = true;
                running = true;
                return;
            }

            try
            {
                // Skip the title screen / demo-attract sequence entirely and
                // drop straight into Episode 1 Map 1 (see -warp note below).
                //
                // -nomusic: ManagedDoom.Unity.UnityMusic streams the level's
                // MIDI-converted music through a Unity AudioClip.PCMReaderCallback
                // (a delegate taking float[]). Il2CppInterop can't marshal a raw
                // float[] delegate parameter across the IL2CPP boundary and
                // throws ArgumentException the moment the level tries to start
                // its music - which happens mid-construction of World, leaving
                // DoomGame.world permanently null and causing an infinite
                // NullReferenceException loop every frame after. -nomusic makes
                // ManagedDoom skip constructing the music system altogether
                // (Doom's own constructor substitutes a no-op NullMusic), which
                // sidesteps the crash without touching sound effects - those
                // go through UnitySound, a separate path that's already working.
                doomGame = new UnityDoom(new CommandLineArgs(new[] { "-warp", "1", "1", "-nomusic" }), transform);

                // Set the properties that must be true to start running first.
                doomGame.AllowInput = true;
                doomGame.Visible = true;
                running = true;

                // Do not set Volume here; avoid touching music when it was deliberately disabled.
            }
            catch (Exception e)
            {
                Plugin.InstanceLog.LogError($"Failed to start Doom: {e}");
            }
        }

        public void Close()
        {
            if (doomGame == null) return;
            doomGame.Visible = false;
            doomGame.AllowInput = false;
            running = false;
        }

        public void Shutdown()
        {
            running = false;
            if (doomGame != null)
            {
                doomGame.Dispose();
                doomGame = null;
            }
        }

        private void Update()
        {
            if (!running || doomGame == null) return;

            // UnityDoom.Run returns false when the internal quit sequence completes
            // (e.g. player backs all the way out of Doom's own menu).
            if (!doomGame.Run(Time.unscaledDeltaTime))
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}