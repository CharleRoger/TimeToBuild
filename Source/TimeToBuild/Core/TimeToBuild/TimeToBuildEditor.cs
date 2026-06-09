using UnityEngine;
using static TimeToBuild.MiscUtils;

namespace TimeToBuild
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class TimeToBuildEditor : TimeToBuild
    {
        protected void OnSave(ConfigNode node)
        {
            if (!LaunchScheduler.LaunchScheduled) return;

            base.OnSave(node);

            if (!ScrapYardUseInventory) return;

            var buildParts = BuildFacility.GatherBuildParts(EditorLogic.fetch.ship.parts);
            foreach (var buildPart in buildParts)
            {
                if (buildPart.ReuseFromInventory) ScrapYard.ScrapYard.Instance.TheInventory.RemovePart(buildPart.ID);
            }
        }

        protected new void Start()
        {
            base.Start();

            if (Scenario.EditorStartTime > 0 && SceneTracker.RevertedFromFlight) LaunchScheduler.ResetTime();

            Scenario.EditorStartTime = CurrentTime;
        }

        private void OnEditorExit()
        {
            LaunchScheduler.ResetTime();
        }

        protected override void HandleButtons()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            EditorLogic.fetch.launchBtn.onClick.RemoveAllListeners();
            EditorLogic.fetch.launchBtn.onClick.AddListener(() => OnLaunchButtonClicked());

            EditorLogic.fetch.exitBtn.onClick.AddListener(() => OnEditorExit());

            var launchSiteSelector = GetMember<GameObject>(EditorLogic.fetch, "launchSiteSelector");
            foreach (var button in launchSiteSelector.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                button.interactable = false;
            }
        }

        protected override void OnLaunchButtonClicked()
        {
            LaunchScheduler.ResetTime();

            BuildFacility buildFacility = null;
            if (EditorDriver.editorFacility == EditorFacility.VAB) buildFacility = Scenario.BuildFacilityVAB;
            if (EditorDriver.editorFacility == EditorFacility.SPH) buildFacility = Scenario.BuildFacilitySPH;
            if (buildFacility is null) return;

            buildFacility.SpawnBuildDialog();
        }
    }
}