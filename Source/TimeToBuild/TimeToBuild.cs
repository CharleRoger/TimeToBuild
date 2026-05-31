using System;
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
        protected abstract void SpawnBuildDialog();

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
        }

        protected void CloseBuildDialog()
        {
            LaunchScheduler.UnsetLaunchTime();
        }
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class TimeToBuildEditor : TimeToBuild
    {
        protected void OnSave(ConfigNode node)
        {
            if (!LaunchScheduler.LaunchScheduled) return;

            LaunchScheduler.WarpToLaunchTime();

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

            Scenario.EditorStartTime = HighLogic.CurrentGame.flightState.universalTime;

            GameEvents.onGameStateSave.Add(OnSave);
        }

        protected void OnDestroy()
        {
            GameEvents.onGameStateSave.Remove(OnSave);
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
            EditorLogic.fetch.launchBtn.onClick.AddListener(() => SpawnBuildDialog());

            EditorLogic.fetch.exitBtn.onClick.AddListener(() => OnEditorExit());

            var launchSiteSelector = GetMember<GameObject>(EditorLogic.fetch, "launchSiteSelector");
            launchSiteSelector.SetActive(false);
        }

        protected override void SpawnBuildDialog()
        {
            LaunchScheduler.ResetTime();

            BuildFacility buildFacility = null;
            if (EditorDriver.editorFacility == EditorFacility.VAB) buildFacility = Scenario.BuildFacilityVAB;
            if (EditorDriver.editorFacility == EditorFacility.SPH) buildFacility = Scenario.BuildFacilitySPH;
            if (buildFacility is null) return;

            var buildParts = BuildFacility.GatherBuildParts(EditorLogic.fetch.ship.parts);

            var buildChunks = buildFacility.ComputeBuildChunks(buildParts);

            var title = "";
            var totalBuildTime = 0;
            foreach (var buildChunk in buildChunks)
            {
                var buildTimeConfig = Profile.BuildTimes[buildChunk.Identifier];

                if (buildChunk.Work > 0)
                {
                    title += buildTimeConfig.Title;

                    if (!Profile.BuildTimes.ContainsKey(buildChunk.Identifier)) continue;

                    var buildRates = LaunchScheduler.GetBuildRates();

                    var rate = buildRates[buildChunk.Identifier];

                    var buildTime = Convert.ToInt32(Math.Ceiling(buildChunk.Work / rate + buildChunk.Overhead));
                    if (buildTime < 0) buildTime = 0;
                    buildTime = LaunchScheduler.Calendar.RoundDuration(buildTime);

                    totalBuildTime += buildTime;

                    var numNewParts = buildParts.Count(buildPart => !buildPart.ReuseFromInventory);
                    var numReusedParts = buildParts.Count(buildPart => buildPart.ReuseFromInventory);

                    var newPartsRelevant = buildTimeConfig.PerNewPart && numNewParts > 0;
                    var reusedPartsRelevant = buildTimeConfig.PerReusedPart && numReusedParts > 0;

                    if (newPartsRelevant || reusedPartsRelevant)
                    {
                        title += " (";
                        if (newPartsRelevant) title += numNewParts.ToString() + " " + (numNewParts > 1 ? LocalizerCache.NewParts : LocalizerCache.NewPart);
                        if (newPartsRelevant && reusedPartsRelevant) title += ", ";
                        if (reusedPartsRelevant) title += numReusedParts.ToString() + " " + (numReusedParts > 1 ? LocalizerCache.ReusedParts : LocalizerCache.ReusedPart);
                        title += ")";
                    }

                    title += "\n" + LaunchScheduler.Calendar.GetDurationString(buildTime) + "\n\n";
                }
            }
            title += LocalizerCache.Total + "\n" + LaunchScheduler.Calendar.GetDurationString(totalBuildTime) + "\n\n";

            LaunchScheduler.SetBuildTime(totalBuildTime);

            var message = "";
            foreach (var date in LaunchScheduler.GetSalientDates()) message += LaunchScheduler.Calendar.GetDateString(date.Item1) + " — " + date.Item2 + "\n";
            
            var optionStartConstruction = GetBuildDialogButton(LocalizerCache.StartBuild, buildFacility.OnStartBuild);
            var optionWarpToEarliestLaunch = GetBuildDialogButton(LocalizerCache.WarpToEarliestLaunch + "\n" + LaunchScheduler.Calendar.GetDateString(LaunchScheduler.LaunchTimeEarliest), buildFacility.OnTryLaunchEarliest);
            var optionWarpToNextMorning = GetBuildDialogButton(LocalizerCache.WarpToNextMorning + "\n" + LaunchScheduler.Calendar.GetDateString(LaunchScheduler.LaunchTimeNextMorning), buildFacility.OnTryLaunchNextMorning);

            SpawnMultiOptionDialog(title, message, optionStartConstruction, optionWarpToEarliestLaunch, optionWarpToNextMorning);
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
            var button = GetMember<UnityEngine.UI.Button>(VesselSpawnDialog.Instance, "buttonLaunch");
            if (!(button is null)) button.interactable = false;

            var launchSiteSelector = GetMember<GameObject>(VesselSpawnDialog.Instance, "launchSiteSelector");
            launchSiteSelector.SetActive(false);
        }

        protected override void SpawnBuildDialog()
        {
            // Not used yet
        }
    }
}