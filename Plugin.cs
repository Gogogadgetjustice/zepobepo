using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.SceneManagement;

namespace BigWalkDoom
{
    [BepInPlugin("com.yourname.bigwalkdoom", "Big Walk Doom", "0.1.0")]
    public class Plugin : BasePlugin
    {
        // This must be here, public, and static!
        public static ManualLogSource InstanceLog;

        public override void Load()
        {
            InstanceLog = base.Log;

            ClassInjector.RegisterTypeInIl2Cpp<DoomCheatTrigger>();
            ClassInjector.RegisterTypeInIl2Cpp<DoomOverlay>();

            string assemblyPath = System.IO.Path.GetDirectoryName(this.GetType().Assembly.Location);
            ManagedDoom.ConfigUtilities.OverrideExeDirectory = assemblyPath;

            SceneManager.sceneLoaded += new System.Action<Scene, LoadSceneMode>((scene, mode) =>
            {
                DoomCheatTrigger.SpawnAtScreen();
            });

            InstanceLog.LogInfo("Big Walk Doom loaded. iddqd.");
        }
    }
}
