using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static TimeToBuild.TimeToBuildProfile;
using static TimeToBuild.TimeToBuildUtils;
using KSP.UI.Screens;
using KSP.Localization;

namespace TimeToBuild
{
    public abstract class TimeToBuild : MonoBehaviour
    {
        public static TimeToBuild Instance = null;
        public TimeToBuildProfile Profile { get; private set; }
        public TimeToBuildScenario Scenario { get; private set; }
        public Calendar Calendar { get; private set; }
        public LaunchScheduler LaunchScheduler { get; private set; } = new LaunchScheduler();

        protected abstract List<SpaceCenterFacility> GetUsingFacilities();
        protected abstract void HandleButtons();
        protected abstract void OnLaunchButtonClicked();

        private IEnumerator InitialiseCalendar_Coroutine()
        {
            while (SpaceCenter.Instance is null || SpaceCenter.Instance.cb is null) yield return new WaitForFixedUpdate();

            Calendar = new Calendar(SpaceCenter.Instance.cb);
        }

        protected void Start()
        {
            Instance = this;

            var settings = HighLogic.CurrentGame.Parameters.CustomParams<TimeToBuildSettings>();
            Profile = GetTimeToBuildProfile(settings.Profile);

            Scenario = HighLogic.CurrentGame.scenarios.FirstOrDefault(s => s.moduleRef is TimeToBuildScenario)?.moduleRef as TimeToBuildScenario;

            if (Scenario is null || Scenario.BuildFacilityVAB is null || Scenario.BuildFacilitySPH is null) return;

            StartCoroutine(InitialiseCalendar_Coroutine());

            GameEvents.onGameStateSave.Add(OnSave);
        }

        protected void Update()
        {
            HandleButtons();
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

        public Dictionary<WorkTime.WorkTimeIdentifier, double> GetBuildRates()
        {
            var buildRates = new Dictionary<WorkTime.WorkTimeIdentifier, double>();

            var timeUnitVariables = Calendar.GetTimeUnitVariables();
            var facilityVariables = GetFacilityVariables();

            foreach (var buildTime in Profile.BuildTimes)
            {
                var facility = buildTime.Key.Facility;

                var facilityVariable = new Dictionary<string, double>();
                facilityVariable["facility_level"] = GetFacilityLevel(buildTime.Key.Facility);
                buildRates[buildTime.Key] = FormulaParser.ParseAndComputeFormula(buildTime.Value.TimeFormula.Rate, timeUnitVariables, facilityVariables, facilityVariable);
            }

            return buildRates;
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

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class TimeToBuildSpaceCentre : TimeToBuild
    {
        bool VesselSpawnDialogIsActive => HighLogic.LoadedSceneIsGame && !(VesselSpawnDialog.Instance is null) && VesselSpawnDialog.Instance.isActiveAndEnabled;
        RDNode SelectedRDNode => HighLogic.LoadedSceneIsGame && !(RDController.Instance is null) && RDController.Instance.isActiveAndEnabled ? RDController.Instance.node_selected : null;

        protected override List<SpaceCenterFacility> GetUsingFacilities()
        {
            var usingFacilities = new List<SpaceCenterFacility>();

            if (VesselSpawnDialogIsActive)
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

        protected override void HandleButtons()
        {
            if (VesselSpawnDialogIsActive)
            {
                var launchButton = GetMember<UnityEngine.UI.Button>(VesselSpawnDialog.Instance, "buttonLaunch");
                if (!(launchButton is null)) launchButton.interactable = false;

                var launchSiteSelector = GetMember<GameObject>(VesselSpawnDialog.Instance, "launchSiteSelector");
                foreach (var button in launchSiteSelector.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    button.interactable = false;
                }
            }

            if (SelectedRDNode)
            {
                foreach (var button in RDController.Instance.techTree.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    if (button.targetGraphic.mainTexture.name == "R&D_btn_research_normal")
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => OnResearchButtonClicked());
                    }
                }
            }
        }

        private void UnlockTech(RDTech tech)
        {
            if (SelectedRDNode is null) return;

            tech.host.AddScience(-tech.scienceCost, TransactionReasons.RnDTechResearch);
            tech.UnlockTech(!(tech.host is null));
        }

        private void StartResearchTech(RDTech tech)
        {
            // Just unlock immediately for now
            UnlockTech(tech);

            SelectedRDNode.UpdateGraphics();
            RDController.Instance.UpdatePanel();
            RDController.Instance.partList.Refresh();
        }

        private RDTech.OperationResult TryResearchTech(RDTech tech)
        {
            var operationResult = RDTech.OperationResult.Successful;

            var scienceCostLimit = GameVariables.Instance.GetScienceCostLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment));

            if (tech.state == RDTech.State.Available)
            {
                Debug.LogError("[RDTech]: Node is already available", tech.gameObject);

                operationResult = RDTech.OperationResult.Failure;
            }
            else if (!CurrencyModifierQuery.RunQuery(TransactionReasons.RnDTechResearch, 0, -tech.scienceCost, 0).CanAfford(c =>
            {
                Debug.Log(StringBuilderCache.Format("[RDTech]: Not enough {0} to research this node.", c), tech.gameObject);
                ScreenMessages.PostScreenMessage(StringBuilderCache.Format(Localizer.Format("#autoLOC_299393", c.Description())), 3, ScreenMessageStyle.UPPER_CENTER);
            }))
            {
                operationResult = RDTech.OperationResult.NotEnoughFunds;
            }
            else if (tech.scienceCost > scienceCostLimit)
            {
                Debug.Log("[RDTech]: Node exceeds Science cost limit.", tech.gameObject);
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_299404", scienceCostLimit.ToString("N0")), 3, ScreenMessageStyle.UPPER_CENTER);

                operationResult = RDTech.OperationResult.ScienceCostLimitExceeded;
            }
            else
            {
                StartResearchTech(tech);
            }

            GameEvents.OnTechnologyResearched.Fire(new GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>(tech, operationResult));

            return operationResult;
        }

        private void OnResearchButtonClicked()
        {
            if (SelectedRDNode is null) return;

            TryResearchTech(SelectedRDNode.tech);
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