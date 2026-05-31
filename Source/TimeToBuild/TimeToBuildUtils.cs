using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TimeToBuild
{
    public static class TimeToBuildUtils
    {
        public static readonly string VariableDryMass = "dry_mass";
        public static readonly string VariableWetMass = "wet_mass";
        public static readonly string VariableDryCost = "dry_cost";
        public static readonly string VariableWetCost = "wet_cost";
        public static readonly string VariableNumBuilds = "num_builds";
        public static readonly string VariableNumParts = "num_parts";

        public struct BuildPart
        {
            public uint ID;
            public bool ReuseFromInventory;
            public double DryMass;
            public double WetMass;
            public double DryCost;
            public double WetCost;
            public int NumBuilds;
        }

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

        public static float GetFacilityLevel(SpaceCenterFacility facility)
        {
            return HighLogic.CurrentGame.Mode != Game.Modes.CAREER ? 1 : ScenarioUpgradeableFacilities.GetFacilityLevel(facility);
        }

        public static Dictionary<string, double> GetFacilityVariables()
        {
            var variables = new Dictionary<string, double>();

            variables["Administration_level"] = GetFacilityLevel(SpaceCenterFacility.Administration);
            variables["AstronautComplex_level"] = GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
            variables["Launchpad_level"] = GetFacilityLevel(SpaceCenterFacility.LaunchPad);
            variables["MissionControl_level"] = GetFacilityLevel(SpaceCenterFacility.MissionControl);
            variables["ResearchAndDevelopment_level"] = GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment);
            variables["Runway_level"] = GetFacilityLevel(SpaceCenterFacility.Runway);
            variables["TrackingStation_level"] = GetFacilityLevel(SpaceCenterFacility.TrackingStation);
            variables["SpaceplaneHangar_level"] = GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar);
            variables["VehicleAssemblyBuilding_level"] = GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding);

            return variables;
        }

        public static Dictionary<BuildTime.BuildTimeIdentifier, double> GetBuildRates(Calendar calendar, IEnumerable<BuildTime> buildTimes)
        {
            var buildRates = new Dictionary<BuildTime.BuildTimeIdentifier, double>();

            var timeUnitVariables = calendar.GetTimeUnitVariables();
            var facilityVariables = GetFacilityVariables();

            foreach (var buildTime in buildTimes)
            {
                var facility = buildTime.Identifier.Facility;

                var facilityVariable = new Dictionary<string, double>();
                facilityVariable["facility_level"] = GetFacilityLevel(buildTime.Identifier.Facility);
                buildRates[buildTime.Identifier] = FormulaParser.ParseAndComputeFormula(buildTime.RateFormula, timeUnitVariables, facilityVariables, facilityVariable);
            }

            return buildRates;
        }

        public static Dictionary<string, double> GetPartVariables(BuildPart buildPart)
        {
            var variables = new Dictionary<string, double>();

            variables[VariableDryMass] = buildPart.DryMass;
            variables[VariableWetMass] = buildPart.WetMass;
            variables[VariableDryCost] = buildPart.DryCost;
            variables[VariableWetCost] = buildPart.WetCost;
            variables[VariableNumBuilds] = buildPart.NumBuilds;

            return variables;
        }

        public static T GetMember<T>(object obj, string memberName)
        {
            var memberInfo = obj.GetType().GetMember(memberName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy).FirstOrDefault();
            if (!(memberInfo is null))
            {
                var member = memberInfo is FieldInfo ? ((FieldInfo)memberInfo).GetValue(obj) : ((PropertyInfo)memberInfo).GetValue(obj, null);
                if (member is T) return (T)member;
            }

            return default;
        }

        public static TimeToBuildScenario GetScenarioModule()
        {
            return HighLogic.CurrentGame.scenarios.FirstOrDefault(s => s.moduleRef is TimeToBuildScenario)?.moduleRef as TimeToBuildScenario;
        }

        public static DialogGUIButton GetBuildDialogButton(string optionText, Callback callback = null)
        {
            return new DialogGUIButton(optionText, callback, 300, 40, true);
        }

        public static void SpawnMultiOptionDialog(string title, string message, params DialogGUIBase[] optionButtons)
        {
            var optionClose = GetBuildDialogButton(LocalizerCache.Close);

            var allOptionButtons = optionButtons.ToList();
            allOptionButtons.Add(optionClose);

            var dialog = new MultiOptionDialog("TimeToBuildDialog", message, title, HighLogic.UISkin, allOptionButtons.ToArray());

            PopupDialog.SpawnPopupDialog(dialog, false, HighLogic.UISkin);
        }
    }
}