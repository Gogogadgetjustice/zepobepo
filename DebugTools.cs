using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using KeyCode = UnityEngine.KeyCode;
using Input = UnityEngine.Input;
using Vector3 = UnityEngine.Vector3;

namespace DebugTools
{
    /// <summary>
    /// Drop this file into any BepInEx IL2CPP plugin project (needs BepInEx.Core,
    /// BepInEx.Unity.IL2CPP, and 0Harmony references, same as any mod). Ships as
    /// its own tiny plugin so it can travel between projects untouched.
    ///
    /// Default keybinds (change in Awake if they clash with the game):
    ///   F6  - dump the active scene's GameObject hierarchy to the log
    ///   F7  - raycast from the main camera, log everything the ray hits
    ///   F8  - list every collider within a radius of the local camera
    ///   F9  - toggle "interact call" logging (Harmony-instruments every method
    ///         with "Interact" in its name across all loaded assemblies, so
    ///         pressing your game's interact button while this is ON will log
    ///         exactly which method(s) fired)
    ///   F10 - dump full component list + serialized-looking fields for whatever
    ///         F7's last raycast hit
    ///  Fair Warning: This will give you a lot of noise in the signal. The game does not discriminate about its things. 
    /// </summary>
    /// unblock the following if you want this as its own thing for some reason, but you can also just copy/paste the class into your own plugin and call DebugToolsPlugin.OnUpdate() from your Update() method.
//    [BepInPlugin("com.gogogadgetjustice.debugtools", "Debug Tools", "0.1.0")]
    public class DebugToolsPlugin : BasePlugin
    {
        public static ManualLogSource DebugLog;
        private Harmony harmony;
        private bool interactLoggingOn;
        private GameObject lastRaycastHit;

        private KeyCode dumpHierarchyKey = KeyCode.F6;
        private KeyCode raycastKey = KeyCode.F7;
        private KeyCode nearbyKey = KeyCode.F8;
        private KeyCode toggleInteractLogKey = KeyCode.F9;
        private KeyCode dumpLastHitKey = KeyCode.F10;
        private float nearbyRadius = 5f;
        public override void Load()
        {
            // Fix assignment error
            DebugLog = base.Log;
            harmony = new Harmony("com.yourname.debugtools");

            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<DebugToolsRunner>();
            var go = new GameObject("DebugToolsRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<DebugToolsRunner>();
            runner.Owner = this;

            DebugLog.LogInfo("DebugTools loaded. F6=hierarchy, F7=raycast, F8=nearby, F9=toggle interact log, F10=dump last hit.");
        }

        // ---- Public entry points called by DebugToolsRunner.Update ----

        public void OnUpdate()
        {
            if (Input.GetKeyDown(dumpHierarchyKey)) DumpHierarchy();
            if (Input.GetKeyDown(raycastKey)) DoRaycast();
            if (Input.GetKeyDown(nearbyKey)) DumpNearby();
            if (Input.GetKeyDown(toggleInteractLogKey)) ToggleInteractLogging();
            if (Input.GetKeyDown(dumpLastHitKey)) DumpLastHit();
        }

        // ---- 1. Scene hierarchy dump ----

        public void DumpHierarchy()
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Hierarchy dump: scene '{scene.name}' ===");
            foreach (var root in scene.GetRootGameObjects())
            {
                DumpRecursive(root.transform, 0, sb);
            }
            Log.LogInfo(sb.ToString());
        }

        private void DumpRecursive(Transform t, int depth, StringBuilder sb)
        {
            var indent = new string(' ', depth * 2);
            var pos = t.position;
            sb.AppendLine($"{indent}{t.name}  pos=({pos.x:F2}, {pos.y:F2}, {pos.z:F2})  active={t.gameObject.activeSelf}");
            for (int i = 0; i < t.childCount; i++)
            {
                DumpRecursive(t.GetChild(i), depth + 1, sb);
            }
        }

        // ---- 2. Raycast from camera ----

        public void DoRaycast()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Log.LogWarning("DoRaycast: no Camera.main found.");
                return;
            }

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                lastRaycastHit = hit.collider != null ? hit.collider.gameObject : null;
                var path = GetHierarchyPath(hit.transform);
                Log.LogInfo(
                    $"Raycast hit '{hit.collider.gameObject.name}' (path: {path}) " +
                    $"at ({hit.point.x:F2}, {hit.point.y:F2}, {hit.point.z:F2}) " +
                    $"distance={hit.distance:F2} normal=({hit.normal.x:F2},{hit.normal.y:F2},{hit.normal.z:F2})");
            }
            else
            {
                Log.LogInfo("Raycast hit nothing within 500m.");
            }
        }

        // ---- 3. Nearby objects ----

        public void DumpNearby()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var origin = cam.transform.position;
            var colliders = Physics.OverlapSphere(origin, nearbyRadius);
            var sb = new StringBuilder();
            sb.AppendLine($"=== {colliders.Length} colliders within {nearbyRadius}m of camera ===");
            foreach (var col in colliders.OrderBy(c => Vector3.Distance(origin, c.transform.position)))
            {
                var dist = Vector3.Distance(origin, col.transform.position);
                sb.AppendLine($"  {col.gameObject.name}  dist={dist:F2}  path={GetHierarchyPath(col.transform)}");
            }
            Log.LogInfo(sb.ToString());
        }

        // ---- 4. Shotgun "interact" call logger ----
        // Patches every method containing "interact" (case-insensitive) in its
        // name, across every loaded, non-dynamic assembly, with a Prefix that
        // only asks Harmony for __instance/__originalMethod - so it works
        // regardless of the method's real parameter list.

        public void ToggleInteractLogging()
        {
            if (interactLoggingOn)
            {
                harmony.UnpatchSelf();
                interactLoggingOn = false;
                Log.LogInfo("Interact-call logging OFF.");
                return;
            }

            int patched = 0;
            var prefix = new HarmonyMethod(typeof(DebugToolsPlugin).GetMethod(nameof(LogInteractCall)));

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }

                foreach (var type in types)
                {
                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static
                        | BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly);
                    }
                    catch { continue; }

                    foreach (var method in methods)
                    {
                        if (!method.Name.Contains("interact", StringComparison.OrdinalIgnoreCase)) continue;
                        if (method.IsAbstract || method.ContainsGenericParameters) continue;

                        try
                        {
                            harmony.Patch(method, prefix: prefix);
                            patched++;
                        }
                        catch
                        {
                            // Skip methods Harmony can't touch (open generics, some IL2CPP
                            // icalls, etc.) - not fatal to the scan.
                        }
                    }
                }
            }

            interactLoggingOn = true;
            Log.LogInfo($"Interact-call logging ON — patched {patched} method(s). Go press the button now.");
        }

        public static void LogInteractCall(object __instance, MethodBase __originalMethod)
        {
            var instanceName = (__instance as UnityEngine.Object)?.name ?? __instance?.GetType().Name ?? "static";
            DebugLog.LogInfo($"[interact-call] {__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name} on '{instanceName}'");
        }

        // ---- 5. Dump components/fields of last raycast hit ----

        public void DumpLastHit()
        {
            if (lastRaycastHit == null)
            {
                Log.LogInfo("No raycast hit recorded yet - press F7 while looking at something first.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Components on '{lastRaycastHit.name}' (path: {GetHierarchyPath(lastRaycastHit.transform)}) ===");
            foreach (var comp in lastRaycastHit.GetComponents<Component>())
            {
                if (comp == null) continue;
                sb.AppendLine($"  [{comp.GetType().FullName}]");
                foreach (var field in comp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    object val;
                    try { val = field.GetValue(comp); } catch { continue; }
                    sb.AppendLine($"      {field.Name} = {val}");
                }
            }
            Log.LogInfo(sb.ToString());
        }

        private static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }
    }

    /// <summary>Tiny MonoBehaviour whose only job is to forward Update() calls,
    /// since IL2CPP plugin entry points (BasePlugin) don't get their own Update.</summary>
    public class DebugToolsRunner : MonoBehaviour
    {
        public DebugToolsRunner(IntPtr ptr) : base(ptr) { }
        public DebugToolsPlugin Owner;

        private void Update()
        {
            Owner?.OnUpdate();
        }
    }
}