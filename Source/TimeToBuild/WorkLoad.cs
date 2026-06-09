using System.Collections.Generic;
using static TimeToBuild.TimeToBuildUtils;

namespace TimeToBuild
{
    public class WorkLoad
    {
        [Persistent]
        public double StartTime { get; private set; } = 0;
        [Persistent]
        public double LastUpdateTime { get; private set; } = 0;
        [Persistent]
        public double WorkDone { get; private set; } = 0;
        [Persistent]
        public List<WorkChunk> WorkChunks { get; private set; } = new List<WorkChunk>();
        [Persistent]
        public BuildVessel BuildVessel { get; private set; } = null;

        public WorkLoad(double startTime, List<WorkChunk> workChunks)
        {
            StartTime = startTime;
            LastUpdateTime = startTime;
            WorkDone = 0;
            WorkChunks = workChunks;
        }

        public WorkLoad(double startTime, List<WorkChunk> workChunks, BuildVessel buildVessel) : this(startTime, workChunks)
        {
            BuildVessel = buildVessel;
        }

        public WorkLoad(ConfigNode node)
        {
            if (node.HasValue("StartTime")) StartTime = double.Parse(node.GetValue("StartTime"));
            if (node.HasValue("LastUpdateTime")) LastUpdateTime = double.Parse(node.GetValue("LastUpdateTime"));
            if (node.HasValue("WorkDone")) WorkDone = double.Parse(node.GetValue("WorkDone"));

            foreach (var workChunkNode in node.GetNodes("WorkChunk")) WorkChunks.Add(new WorkChunk(workChunkNode));

            if (node.HasNode("BuildVessel")) BuildVessel = new BuildVessel(node.GetNode("BuildVessel"));
        }

        public ConfigNode GetConfigNode()
        {
            ConfigNode node = new ConfigNode();

            node.AddValue("StartTime", StartTime);
            node.AddValue("LastUpdateTime", LastUpdateTime);
            node.AddValue("WorkDone", WorkDone);

            foreach (var workChunkNode in WorkChunks)
            {
                node.AddNode("WorkChunk", workChunkNode.GetConfigNode());
            }

            if (BuildVessel != null)
            {
                node.AddNode("BuildVessel", BuildVessel.GetConfigNode());
            }

            return node;
        }

        public bool UpdateWorkDone(Dictionary<BuildTime.BuildTimeIdentifier, double> buildRates)
        {
            if (HighLogic.LoadedSceneIsEditor) return false;

            var currentTime = CurrentTime;

            var totalWorkDoneByLastUpdate = WorkDone;
            WorkDone = 0;
            var workDoneOnPreviousChunks = 0.0;
            var mostRecentChunkCompletetionTime = StartTime;
            var chunkDone = false;
            foreach (var workChunk in WorkChunks)
            {
                var workDoneOnThisChunk = 0.0;

                chunkDone = workChunk.CompletionTime > 0;

                var workDoneByLastUpdate = workChunk.Work;
                var workDoneSinceLastUpdate = 0.0;
                if (!chunkDone)
                {
                    // Work chunk not completed at last update

                    var chunkWorkStartTime = mostRecentChunkCompletetionTime + workChunk.Overhead;
                    if (chunkWorkStartTime > currentTime)
                    {
                        // Work chunk still in constant overhead phase
                        continue;
                    }
                    else
                    {
                        // Work chunk has started variable rate work phase

                        workDoneByLastUpdate = totalWorkDoneByLastUpdate - workDoneOnPreviousChunks;
                        var timeSinceLastUpdate = currentTime - LastUpdateTime;
                        workDoneSinceLastUpdate = timeSinceLastUpdate * buildRates[workChunk.Identifier];
                        workDoneOnThisChunk = workDoneByLastUpdate + workDoneSinceLastUpdate;
                        chunkDone = workDoneOnThisChunk > workChunk.Work;
                    }
                }

                if (chunkDone) workDoneOnThisChunk = workChunk.Work;

                workDoneSinceLastUpdate = workDoneOnThisChunk - workDoneByLastUpdate;

                WorkDone += workDoneOnThisChunk;

                var timeSpentOnChunkSinceLastUpdate = workChunk.Overhead + workDoneSinceLastUpdate / buildRates[workChunk.Identifier];

                if (chunkDone)
                {
                    // Work chunk completed by current time

                    if (workChunk.CompletionTime < 0)
                    {
                        workChunk.CompletionTime = LastUpdateTime + timeSpentOnChunkSinceLastUpdate;
                    }

                    mostRecentChunkCompletetionTime = workChunk.CompletionTime;
                    workDoneOnPreviousChunks += workChunk.Work;

                    if (workDoneOnPreviousChunks > totalWorkDoneByLastUpdate)
                    {
                        LastUpdateTime = workChunk.CompletionTime;
                        totalWorkDoneByLastUpdate = workDoneOnPreviousChunks;
                    }
                }
                else
                {
                    // Work chunk in progress, stop processing here
                    LastUpdateTime = currentTime;
                    break;
                }
            }

            return chunkDone;
        }
    }
}
