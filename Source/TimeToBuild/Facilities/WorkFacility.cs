using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TimeToBuild
{
    public abstract class WorkFacility
    {
        public List<WorkLoad> WorkLoads { get; private set; } = new List<WorkLoad>();
        public SpaceCenterFacility Facility { get; private set; }
        public List<SpaceCenterFacility> UsingFacilities { get; private set; } = new List<SpaceCenterFacility>();

        public WorkFacility(SpaceCenterFacility facility)
        {
            Facility = facility;

            UsingFacilities.Add(facility);
            if (facility == SpaceCenterFacility.VehicleAssemblyBuilding) UsingFacilities.Add(SpaceCenterFacility.LaunchPad);
            if (facility == SpaceCenterFacility.SpaceplaneHangar) UsingFacilities.Add(SpaceCenterFacility.Runway);
        }

        public abstract void OnWorkLoadComplete(WorkLoad workLoad);

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
    }
}
