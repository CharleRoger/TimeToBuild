using UnityEngine;
using static TimeToBuild.MiscUtils;
using KSP.UI.Screens;

namespace TimeToBuild
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class TimeToBuildManagerSpaceCentre : TimeToBuildManager
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

        private void OnResearchButtonClicked()
        {
            if (Scenario is null || Scenario.ResearchFacility is null || SelectedRDNode is null) return;

            Scenario.ResearchFacility.TryStartResearch(SelectedRDNode);
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