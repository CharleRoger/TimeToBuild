using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static TimeToBuild.TimeToBuildProfile;
using static TimeToBuild.TimeToBuildUtils;
using KSP.UI.Screens;

namespace TimeToBuild
{
    public abstract class TimeToBuild : MonoBehaviour
    {
        public static TimeToBuild Instance = null;

        public TimeToBuildProfile Profile { get; private set; }

        public TimeToBuildScenario Scenario { get; private set; }

        public LaunchScheduler LaunchScheduler { get; private set; }

        protected abstract List<SpaceCenterFacility> GetUsingFacilities();
        protected abstract bool ButtonsAreActive();
        protected abstract void HandleButtons();
        protected abstract void OnLaunchButtonClicked();

        private IEnumerator InitialiseLaunchScheduler_Coroutine()
        {
            while (SpaceCenter.Instance is null || SpaceCenter.Instance.cb is null) yield return new WaitForFixedUpdate();

            LaunchScheduler = new LaunchScheduler(new Calendar(SpaceCenter.Instance.cb));
        }

        private IEnumerator HandleButtons_Coroutine()
        {
            while (true)
            {
                if (ButtonsAreActive()) HandleButtons();
                yield return new WaitForFixedUpdate();
            }
        }

        protected void Start()
        {
            Instance = this;

            var settings = HighLogic.CurrentGame.Parameters.CustomParams<TimeToBuildSettings>();
            Profile = GetTimeToBuildProfile(settings.Profile);

            Scenario = HighLogic.CurrentGame.scenarios.FirstOrDefault(s => s.moduleRef is TimeToBuildScenario)?.moduleRef as TimeToBuildScenario;

            if (Scenario is null || Scenario.BuildFacilityVAB is null || Scenario.BuildFacilitySPH is null) return;

            StartCoroutine(InitialiseLaunchScheduler_Coroutine());

            StartCoroutine(HandleButtons_Coroutine());

            GameEvents.onGameStateSave.Add(OnSave);
        }

        protected void OnSave(ConfigNode node)
        {
            // Bit of a hack, but trying to warp any earlier won't work

            if (LaunchScheduler.LaunchScheduled) LaunchScheduler.WarpToLaunchTime();
        }

        protected void OnDestroy()
        {
            GameEvents.onGameStateSave.Remove(OnSave);
        }
    }
    
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

        protected override List<SpaceCenterFacility> GetUsingFacilities()
        {
            var usingFacilities = new List<SpaceCenterFacility>();

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                usingFacilities.Add(SpaceCenterFacility.VehicleAssemblyBuilding);
                usingFacilities.Add(SpaceCenterFacility.LaunchPad);
            }
            else if (EditorDriver.editorFacility == EditorFacility.SPH)
            {
                usingFacilities.Add(SpaceCenterFacility.SpaceplaneHangar);
                usingFacilities.Add(SpaceCenterFacility.Runway);
            }

            return usingFacilities;
        }

        protected override bool ButtonsAreActive()
        {
            return HighLogic.LoadedSceneIsEditor;
        }

        private void OnEditorExit()
        {
            LaunchScheduler.ResetTime();
        }

        protected override void HandleButtons()
        {
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

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class TimeToBuildSpaceCentre : TimeToBuild
    {
        protected override List<SpaceCenterFacility> GetUsingFacilities()
        {
            var usingFacilities = new List<SpaceCenterFacility>();

            if (ButtonsAreActive())
            {
                var launchSiteFacility = GetMember<LaunchSiteFacility>(VesselSpawnDialog.Instance, "launchSiteFacility");
                if (!(launchSiteFacility is null))
                {
                    if (launchSiteFacility.facilityType == EditorFacility.VAB)
                    {
                        usingFacilities.Add(SpaceCenterFacility.LaunchPad);
                    }
                    else if (launchSiteFacility.facilityType == EditorFacility.SPH)
                    {
                        usingFacilities.Add(SpaceCenterFacility.Runway);
                    }
                }
            }

            return usingFacilities;
        }

        protected override bool ButtonsAreActive()
        {
            return HighLogic.LoadedSceneIsGame && !(VesselSpawnDialog.Instance is null) && VesselSpawnDialog.Instance.isActiveAndEnabled;
        }

        protected override void HandleButtons()
        {
            var launchButton = GetMember<UnityEngine.UI.Button>(VesselSpawnDialog.Instance, "buttonLaunch");
            if (!(launchButton is null)) launchButton.interactable = false;

            var launchSiteSelector = GetMember<GameObject>(VesselSpawnDialog.Instance, "launchSiteSelector");
            foreach (var button in launchSiteSelector.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                button.interactable = false;
            }
        }

        protected override void OnLaunchButtonClicked()
        {
            // Not used yet
        }

        protected void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
    }
}