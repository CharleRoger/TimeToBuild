using System.Collections;
using System.Collections.Generic;
using TimeToBuild.Core;
using TimeToBuild.Utils;
using static TimeToBuild.Utils.MiscUtils;
using TimeToBuild.Work;
using UnityEngine;

namespace TimeToBuild.Facilities
{
    public abstract class WorkFacility
    {
        public List<WorkLoad> WorkLoads { get; private set; } = new List<WorkLoad>();
        public SpaceCenterFacility Facility { get; private set; }
        public List<SpaceCenterFacility> UsingFacilities { get; private set; } = new List<SpaceCenterFacility>();
        protected double CompletionTime { get; private set; }

        public WorkFacility(SpaceCenterFacility facility)
        {
            Facility = facility;

            UsingFacilities.Add(facility);
            if (facility == SpaceCenterFacility.VehicleAssemblyBuilding) UsingFacilities.Add(SpaceCenterFacility.LaunchPad);
            if (facility == SpaceCenterFacility.SpaceplaneHangar) UsingFacilities.Add(SpaceCenterFacility.Runway);
        }

        public abstract List<WorkChunk> ComputeWorkChunks();
        public abstract List<WorkChunk.WorkChunkDatum> GetWorkChunkData();
        public abstract void SpawnWorkCompleteDialog(WorkItem workItem);
        public abstract void OnWorkLoadComplete(WorkLoad workLoad);
        public abstract void OnStartWork();
        public abstract DialogGUIButton[] GetStartWorkDialogButtons();

        public void Load(ConfigNode node)
        {
            foreach (var workLoadNode in node.GetNodes("WorkLoad"))
            {
                WorkLoads.Add(new WorkLoad(workLoadNode));
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var workLoad in WorkLoads)
            {
                node.AddNode("WorkLoad", workLoad.GetConfigNode());
            }
        }

        public void UpdateWorkLoad(int workLoadIndex, Dictionary<WorkTime.WorkTimeIdentifier, double> workRates)
        {
            var workLoad = WorkLoads[workLoadIndex];

            bool workComplete = workLoad.UpdateWorkDone(workRates);

            if (workComplete)
            {
                WorkLoads.RemoveAt(workLoadIndex);

                TimeWarp.SetRate(0, true);

                OnWorkLoadComplete(workLoad);

                if (!(workLoad.Vessel is null)) SpawnWorkCompleteDialog(workLoad.Vessel);
                if (!(workLoad.Tech is null)) SpawnWorkCompleteDialog(workLoad.Tech);
            }
        }

        public IEnumerator UpdateWorkLoads_Coroutine()
        {
            while (TimeToBuildManager.Instance is null || HighLogic.LoadedSceneIsEditor) yield return new WaitForFixedUpdate();

            while (true)
            {
                var workRates = TimeToBuildManager.Instance.GetWorkRates();

                for (int workLoadIndex = 0; workLoadIndex < WorkLoads.Count; workLoadIndex++)
                {
                    UpdateWorkLoad(workLoadIndex, workRates);
                }

                yield return new WaitForFixedUpdate();
            }
        }

        public bool TryAddWorkLoad(WorkLoad workLoad, bool actuallyAddIt)
        {
            if (WorkLoads.Count > 0) return false;

            if (actuallyAddIt) WorkLoads.Add(workLoad);

            return true;
        }

        protected virtual void SetTotalWorkDuration(double workDuration)
        {
            CompletionTime = CurrentTime + workDuration;
        }

        public void SpawnStartWorkDialog()
        {
            var workChunkData = GetWorkChunkData();

            var title = "";
            var totalWorkDuration = 0;
            foreach (var workChunkDatum in workChunkData)
            {
                title += workChunkDatum.Title;

                totalWorkDuration += workChunkDatum.Duration;

                if (workChunkDatum.NewPartCount > 0 || workChunkDatum.ReusedPartCount > 0)
                {
                    title += " (";
                    if (workChunkDatum.NewPartCount > 0) title += workChunkDatum.NewPartCount.ToString() + " " + (workChunkDatum.NewPartCount > 1 ? LocalizerCache.NewParts : LocalizerCache.NewPart);
                    if (workChunkDatum.NewPartCount > 0 && workChunkDatum.ReusedPartCount > 0) title += ", ";
                    if (workChunkDatum.ReusedPartCount > 0) title += workChunkDatum.ReusedPartCount.ToString() + " " + (workChunkDatum.ReusedPartCount > 1 ? LocalizerCache.ReusedParts : LocalizerCache.ReusedPart);
                    title += ")";
                }

                title += "\n" + TimeToBuildManager.Instance.Calendar.GetDurationString(workChunkDatum.Duration) + "\n\n";
            }
            if (workChunkData.Count > 1) title += LocalizerCache.Total + "\n" + TimeToBuildManager.Instance.Calendar.GetDurationString(totalWorkDuration) + "\n\n";

            SetTotalWorkDuration(totalWorkDuration);

            var message = "";
            foreach (var date in TimeToBuildManager.Instance.GetSalientDates(CompletionTime)) message += TimeToBuildManager.Instance.Calendar.GetDateString(date.Item1) + " — " + date.Item2 + "\n";

            SpawnMultiOptionDialog(title, message, GetStartWorkDialogButtons());
        }
    }
}
