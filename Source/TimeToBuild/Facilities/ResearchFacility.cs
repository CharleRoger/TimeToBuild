using UnityEngine;
using KSP.UI.Screens;
using KSP.Localization;

namespace TimeToBuild.Facilities
{
    public class ResearchFacility : WorkFacility
    {
        public ResearchFacility() : base(SpaceCenterFacility.ResearchAndDevelopment)
        {

        }

        public override void OnWorkLoadComplete(WorkLoad workLoad)
        {

        }

        private void UnlockTech(RDTech tech)
        {
            tech.host.AddScience(-tech.scienceCost, TransactionReasons.RnDTechResearch);
            tech.UnlockTech(!(tech.host is null));
        }

        private void StartResearch(RDNode rdNode)
        {
            // Just unlock immediately for now
            UnlockTech(rdNode.tech);

            rdNode.UpdateGraphics();
            RDController.Instance.UpdatePanel();
            RDController.Instance.partList.Refresh();
        }

        public RDTech.OperationResult TryStartResearch(RDNode rdNode)
        {
            var operationResult = RDTech.OperationResult.Successful;

            var scienceCostLimit = GameVariables.Instance.GetScienceCostLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment));

            if (rdNode.tech.state == RDTech.State.Available)
            {
                Debug.LogError("[RDTech]: Node is already available", rdNode.tech.gameObject);

                operationResult = RDTech.OperationResult.Failure;
            }
            else if (!CurrencyModifierQuery.RunQuery(TransactionReasons.RnDTechResearch, 0, -rdNode.tech.scienceCost, 0).CanAfford(c =>
            {
                Debug.Log(StringBuilderCache.Format("[RDTech]: Not enough {0} to research this node.", c), rdNode.tech.gameObject);
                ScreenMessages.PostScreenMessage(StringBuilderCache.Format(Localizer.Format("#autoLOC_299393", c.Description())), 3, ScreenMessageStyle.UPPER_CENTER);
            }))
            {
                operationResult = RDTech.OperationResult.NotEnoughFunds;
            }
            else if (rdNode.tech.scienceCost > scienceCostLimit)
            {
                Debug.Log("[RDTech]: Node exceeds Science cost limit.", rdNode.tech.gameObject);
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_299404", scienceCostLimit.ToString("N0")), 3, ScreenMessageStyle.UPPER_CENTER);

                operationResult = RDTech.OperationResult.ScienceCostLimitExceeded;
            }
            else
            {
                StartResearch(rdNode);
            }

            GameEvents.OnTechnologyResearched.Fire(new GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>(rdNode.tech, operationResult));

            return operationResult;
        }
    }
}
