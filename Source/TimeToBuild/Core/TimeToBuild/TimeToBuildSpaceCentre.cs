using UnityEngine;
using static TimeToBuild.MiscUtils;
using KSP.UI.Screens;
using KSP.Localization;

namespace TimeToBuild
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class TimeToBuildSpaceCentre : TimeToBuild
    {
        bool VesselSpawnDialogIsActive => HighLogic.LoadedSceneIsGame && !(VesselSpawnDialog.Instance is null) && VesselSpawnDialog.Instance.isActiveAndEnabled;
        RDNode SelectedRDNode => HighLogic.LoadedSceneIsGame && !(RDController.Instance is null) && RDController.Instance.isActiveAndEnabled ? RDController.Instance.node_selected : null;

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