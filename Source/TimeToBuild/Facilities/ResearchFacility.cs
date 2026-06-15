using UnityEngine;
using KSP.UI.Screens;
using KSP.Localization;
using static TimeToBuild.Utils.MiscUtils;
using TimeToBuild.Core;
using TimeToBuild.Utils;
using TimeToBuild.Work;
using System.Collections.Generic;
using System;
using System.Linq;

namespace TimeToBuild.Facilities
{
    public class ResearchFacility : WorkFacility
    {
        private ProtoTechNode TechNodeToResearch = null;

        public ResearchFacility() : base(SpaceCenterFacility.ResearchAndDevelopment)
        {

        }

        private void UnlockTech(string techID)
        {
            var protoTechNode = AssetBase.RnDTechTree.FindTech(techID);
            ResearchAndDevelopment.Instance.UnlockProtoTechNode(protoTechNode);

            if (!(RDController.Instance is null))
            {
                foreach (var rdNode in RDController.Instance.nodes)
                {
                    if (rdNode.tech.techID == techID)
                    {
                        rdNode.UpdateGraphics();
                        break;
                    }
                }

                RDController.Instance.UpdatePanel();
                RDController.Instance.partList.Refresh();
            }

            SpawnResearchCompleteDialog(techID);
        }

        public override void OnWorkLoadComplete(WorkLoad workLoad)
        {
            if (workLoad.Tech is null) return;

            UnlockTech(workLoad.Tech.TechID);
        }

        public List<WorkChunk> ComputeResearchWorkChunks()
        {
            var workChunks = new List<WorkChunk>();

            if (UsingFacilities.Count == 0) return workChunks;

            var constants = TimeToBuildManager.Instance.Calendar.GetTimeUnitVariables();
            constants["cost"] = TechNodeToResearch.scienceCost;

            foreach (var researchTime in TimeToBuildManager.Instance.Profile.ResearchTimes.Values)
            {
                if (!UsingFacilities.Contains(researchTime.Identifier.Facility)) continue;

                var facilityVariables = GetFacilityVariables();
                facilityVariables["facility_level"] = GetFacilityLevel(researchTime.Identifier.Facility);

                var workChunk = new WorkChunk(researchTime.Identifier);
                workChunk.Work = FormulaParser.ParseAndComputeFormula(researchTime.TimeFormula.Work, constants, facilityVariables);
                workChunk.Overhead = FormulaParser.ParseAndComputeFormula(researchTime.TimeFormula.Overhead, constants, facilityVariables);

                if (workChunk.Work > 0 || workChunk.Overhead > 0) workChunks.Add(workChunk);
            }

            return workChunks;
        }

        public List<WorkChunk.WorkChunkDatum> GetTechWorkChunkData()
        {
            var workChunkData = new List<WorkChunk.WorkChunkDatum>();

            var workChunks = ComputeResearchWorkChunks();

            var workRates = TimeToBuildManager.Instance.GetWorkRates();

            foreach (var workChunk in workChunks)
            {
                if (!TimeToBuildManager.Instance.Profile.ResearchTimes.ContainsKey(workChunk.Identifier)) continue;

                var researchTimeConfig = TimeToBuildManager.Instance.Profile.ResearchTimes[workChunk.Identifier];

                if (workChunk.Work > 0 || workChunk.Overhead > 0)
                {
                    var workChunkDatum = new WorkChunk.WorkChunkDatum();
                    workChunkDatum.Title = researchTimeConfig.Title;

                    var rate = workRates[workChunk.Identifier];
                    workChunkDatum.Duration = Convert.ToInt32(Math.Ceiling(workChunk.Work / rate + workChunk.Overhead));
                    if (workChunkDatum.Duration < 0) workChunkDatum.Duration = 0;
                    workChunkDatum.Duration = TimeToBuildManager.Instance.Calendar.RoundDuration(workChunkDatum.Duration);

                    workChunkData.Add(workChunkDatum);
                }
            }

            return workChunkData;
        }

        public void SpawnResearchDialog()
        {
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER) return;

            if (TechNodeToResearch is null) return;

            var workChunkData = GetTechWorkChunkData();

            var title = "";
            var totalResearchime = 0;
            foreach (var workChunkDatum in workChunkData)
            {
                title += workChunkDatum.Title;

                totalResearchime += workChunkDatum.Duration;

                title += "\n" + TimeToBuildManager.Instance.Calendar.GetDurationString(workChunkDatum.Duration) + "\n\n";
            }
            if (workChunkData.Count > 1) title += LocalizerCache.Total + "\n" + TimeToBuildManager.Instance.Calendar.GetDurationString(totalResearchime) + "\n\n";

            var completionDate = CurrentTime + totalResearchime;

            var message = "";
            foreach (var date in TimeToBuildManager.Instance.GetSalientDates(completionDate)) message += TimeToBuildManager.Instance.Calendar.GetDateString(date.Item1) + " — " + date.Item2 + "\n";

            var optionStartResearch = GetBuildDialogButton(LocalizerCache.StartResearch, OnStartResearch);

            SpawnMultiOptionDialog(title, message, optionStartResearch);
        }

        private bool TryStartResearch(List<WorkChunk> workChunks, bool actuallyAddIt)
        {
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER) return false;

            var tech = new WorkItemTech(TechNodeToResearch.techID);
            var workLoad = new WorkLoad(CurrentTime, workChunks, tech);
            var success = TryAddWorkLoad(workLoad, actuallyAddIt);

            if (!success) SpawnMultiOptionDialog(LocalizerCache.CannotStartResearch, LocalizerCache.ResearchFacilityBusy);

            return success;
        }

        public static string GetTechTitle(string techID)
        {
            ConfigNode techNode = GameDatabase.Instance.GetConfigNodes("RDNode").FirstOrDefault(t => t.GetValue("id") == techID);

            return techNode is null ? techID : techNode.GetValue("title");
        }

        public void OnStartResearch()
        {
            if (TechNodeToResearch is null) return;

            var workChunks = ComputeResearchWorkChunks();

            var workChunkData = GetTechWorkChunkData();

            var completionTime = CurrentTime;
            foreach (var workChunkDatum in workChunkData) completionTime += workChunkDatum.Duration;
            
            if (TryStartResearch(workChunks, true))
            {
                ResearchAndDevelopment.Instance.AddScience(-TechNodeToResearch.scienceCost, TransactionReasons.RnDTechResearch);

                var techTitle = GetTechTitle(TechNodeToResearch.techID);

                var alarm = new AlarmTypeRaw();
                alarm.ut = completionTime;
                alarm.title = techTitle + " " + LocalizerCache.ResearchCompleteTitle;
                alarm.description = techTitle + " " + LocalizerCache.ResearchCompleteDescription;

                AlarmClockScenario.Instance.alarms.Add((uint)new System.Random().Next(), alarm);
            }
        }

        public void SpawnResearchCompleteDialog(string techID)
        {
            SpawnMultiOptionDialog(LocalizerCache.ResearchComplete, GetTechTitle(techID) + " " + LocalizerCache.NowAvailable);
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
            else if (!(RDController.Instance is null))
            {
                TechNodeToResearch = AssetBase.RnDTechTree.FindTech(rdNode.tech.techID);
                SpawnResearchDialog();
            }

            GameEvents.OnTechnologyResearched.Fire(new GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>(rdNode.tech, operationResult));

            return operationResult;
        }
    }
}
