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
        public List<WorkItem> WorkItems { get; private set; } = new List<WorkItem>();
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

        public abstract void Load(ConfigNode node);
        public abstract ConfigNode Save();
        public abstract List<WorkChunk> ComputeWorkChunks();
        public abstract List<WorkChunk.WorkChunkDatum> GetWorkChunkData();
        public abstract void SpawnWorkItemCompleteDialog(WorkItem workItem);
        public abstract void OnWorkItemComplete(WorkItem workItem);
        public abstract void OnStartWork();
        public abstract DialogGUIButton[] GetStartWorkItemDialogButtons();

        public void UpdateWorkItem(int workItemIndex, Dictionary<WorkTime.WorkTimeIdentifier, double> workRates)
        {
            var workItem = WorkItems[workItemIndex];

            bool workComplete = workItem.UpdateWorkDone(workRates);

            if (workComplete)
            {
                WorkItems.RemoveAt(workItemIndex);

                TimeWarp.SetRate(0, true);

                OnWorkItemComplete(workItem);

                SpawnWorkItemCompleteDialog(workItem);
            }
        }

        public IEnumerator UpdateWorkItems_Coroutine()
        {
            while (TimeToBuildManager.Instance is null || HighLogic.LoadedSceneIsEditor) yield return new WaitForFixedUpdate();

            while (true)
            {
                var workRates = TimeToBuildManager.Instance.GetWorkRates();

                for (int workItemIndex = 0; workItemIndex < WorkItems.Count; workItemIndex++) UpdateWorkItem(workItemIndex, workRates);

                yield return new WaitForFixedUpdate();
            }
        }

        public bool TryAddWorkItem(WorkItem workItem, bool actuallyAddIt)
        {
            if (WorkItems.Count > 0) return false;

            if (actuallyAddIt) WorkItems.Add(workItem);

            return true;
        }

        protected virtual void SetTotalWorkDuration(double workDuration)
        {
            CompletionTime = CurrentTime + workDuration;
        }

        private string GetEventString(double time, string title, bool bold)
        {
            var str = TimeToBuildManager.Instance.Calendar.GetDateString(time) + " — " + title;
            if (bold) str = "<b>" + str + "</b>";
            return str + "\n";
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

            var message = GetEventString(CurrentTime, LocalizerCache.CurrentTime, true);
            foreach (var date in TimeToBuildManager.Instance.GetSalientDates(CompletionTime)) message += GetEventString(date.Item1, date.Item2, false);
            message += GetEventString(CompletionTime, LocalizerCache.CompletionTime, true);

            SpawnMultiOptionDialog(title, message, GetStartWorkItemDialogButtons());
        }
    }
}
