using UnityEngine;

namespace TimeToBuild.Utils
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class SceneTracker : MonoBehaviour
    {
        public static bool RevertedFromFlight;

        private void OnSceneSwitch(GameEvents.FromToAction<GameScenes, GameScenes> data)
        {
            RevertedFromFlight = data.from == GameScenes.FLIGHT && data.to == GameScenes.EDITOR;
        }

        public void Awake()
        {
            DontDestroyOnLoad(this);

            GameEvents.onGameSceneSwitchRequested.Add(OnSceneSwitch);
        }
    }
}
