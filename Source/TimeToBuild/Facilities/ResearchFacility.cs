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

        public override void Load(ConfigNode node)
        {
            foreach (var workItemNode in node.GetNodes("WorkItemTech")) WorkItems.Add(new WorkItemTech(workItemNode));
        }

        public override ConfigNode Save()
        {
            var node = new ConfigNode();

            foreach (var workItem in WorkItems) if (workItem is WorkItemTech) node.AddNode("WorkItemTech", workItem.Save());

            return node;
        }

        public override void OnWorkItemComplete(WorkItem workItem)
        {
            var tech = (WorkItemTech)workItem;
            if (tech is null) return;

            var protoTechNode = AssetBase.RnDTechTree.FindTech(tech.TechID);
            ResearchAndDevelopment.Instance.UnlockProtoTechNode(protoTechNode);

            if (!(RDController.Instance is null))
            {
                foreach (var rdNode in RDController.Instance.nodes)
                {
                    if (rdNode.tech.techID == tech.TechID)
                    {
                        rdNode.UpdateGraphics();
                        break;
                    }
                }

                RDController.Instance.UpdatePanel();
                RDController.Instance.partList.Refresh();
            }
        }

        public override List<WorkChunk> ComputeWorkChunks()
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

        public override List<WorkChunk.WorkChunkDatum> GetWorkChunkData()
        {
            var workChunkData = new List<WorkChunk.WorkChunkDatum>();

            var workChunks = ComputeWorkChunks();

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

        private bool TryStartResearch(List<WorkChunk> workChunks, bool actuallyAddIt)
        {
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER) return false;

            var tech = new WorkItemTech(CurrentTime, workChunks, TechNodeToResearch.techID);
            var success = TryAddWorkItem(tech, actuallyAddIt);

            if (!success) SpawnMultiOptionDialog(LocalizerCache.CannotStartResearch, LocalizerCache.ResearchFacilityBusy);

            return success;
        }

        public static string GetTechTitle(string techID)
        {
            ConfigNode techNode = GameDatabase.Instance.GetConfigNodes("RDNode").FirstOrDefault(t => t.GetValue("id") == techID);

            return techNode is null ? techID : techNode.GetValue("title");
        }

        public override void OnStartWork()
        {
            if (TechNodeToResearch is null) return;

            var workChunks = ComputeWorkChunks();
            var workChunkData = GetWorkChunkData();

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

        public override DialogGUIButton[] GetStartWorkItemDialogButtons()
        {
            return new DialogGUIButton[1]
            {
                GetBuildDialogButton(LocalizerCache.StartResearch, OnStartWork)
            };
        }

        public override void SpawnWorkItemCompleteDialog(WorkItem workItem)
        {
            var tech = (WorkItemTech)workItem;
            if (tech is null) return;

            SpawnMultiOptionDialog(LocalizerCache.ResearchComplete, GetTechTitle(tech.TechID) + " " + LocalizerCache.NowAvailable);
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
                SpawnStartWorkDialog();
            }

            GameEvents.OnTechnologyResearched.Fire(new GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>(rdNode.tech, operationResult));

            return operationResult;
        }
    }
}
