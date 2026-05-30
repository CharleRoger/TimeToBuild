using System.Collections.Generic;

namespace TimeToBuild
{
    public class BuildVessel
    {
        [Persistent]
        public string LaunchSiteName { get; private set; } = "";
        [Persistent]
        public double StartTime { get; private set; } = 0;
        [Persistent]
        public double LastUpdateTime { get; private set; } = 0;
        [Persistent]
        public double WorkDone { get; private set; } = 0;
        [Persistent]
        public List<BuildChunk> BuildChunks { get; private set; } = new List<BuildChunk>();
        [Persistent]
        public ShipConstruct ShipConstruct { get; private set; }

        public BuildVessel(ShipConstruct shipConstruct, string launchSiteName, double startTime, List<BuildChunk> buildChunks)
        {
            LaunchSiteName = launchSiteName;
            ShipConstruct = shipConstruct;
            StartTime = startTime;
            LastUpdateTime = startTime;
            WorkDone = 0;
            BuildChunks = buildChunks;
        }

        public BuildVessel(ConfigNode node)
        {
            if (node.HasValue("LaunchSiteName")) LaunchSiteName = node.GetValue("LaunchSiteName");
            if (node.HasValue("StartTime")) StartTime = double.Parse(node.GetValue("StartTime"));
            if (node.HasValue("LastUpdateTime")) LastUpdateTime = double.Parse(node.GetValue("LastUpdateTime"));
            if (node.HasValue("WorkDone")) WorkDone = double.Parse(node.GetValue("WorkDone"));

            foreach (var buildChunkNode in node.GetNodes("BuildChunk")) BuildChunks.Add(new BuildChunk(buildChunkNode));

            ShipConstruct = new ShipConstruct();
            if (node.HasNode("ShipConstruct")) ShipConstruct.LoadShip(node.GetNode("ShipConstruct"));
        }

        public ConfigNode GetConfigNode()
        {
            ConfigNode node = new ConfigNode();

            node.AddValue("LaunchSiteName", LaunchSiteName);
            node.AddValue("StartTime", StartTime);
            node.AddValue("LastUpdateTime", LastUpdateTime);
            node.AddValue("WorkDone", WorkDone);

            foreach (var buildChunkNode in BuildChunks)
            {
                node.AddNode("BuildChunk", buildChunkNode.GetConfigNode());
            }

            if (!(ShipConstruct is null)) node.AddNode("ShipConstruct", ShipConstruct.SaveShip());

            return node;
        }

        public bool UpdateWorkDone(Dictionary<BuildTime.BuildTimeIdentifier, double> buildRates)
        {
            if (HighLogic.LoadedSceneIsEditor) return false;

            var currentTime = Planetarium.GetUniversalTime();

            var totalWorkDoneByLastUpdate = WorkDone;
            WorkDone = 0;
            var workDoneOnPreviousChunks = 0.0;
            var mostRecentChunkCompletetionTime = StartTime;
            var chunkDone = false;
            foreach (var buildChunk in BuildChunks)
            {
                var workDoneOnThisChunk = 0.0;

                chunkDone = buildChunk.CompletionTime > 0;

                var workDoneByLastUpdate = buildChunk.Work;
                var workDoneSinceLastUpdate = 0.0;
                if (!chunkDone)
                {
                    // Build chunk not completed at last update

                    var chunkWorkStartTime = mostRecentChunkCompletetionTime + buildChunk.Overhead;
                    if (chunkWorkStartTime > currentTime)
                    {
                        // Build chunk still in constant overhead phase
                        continue;
                    }
                    else
                    {
                        // Build chunk has started variable rate work phase

                        workDoneByLastUpdate = totalWorkDoneByLastUpdate - workDoneOnPreviousChunks;
                        var timeSinceLastUpdate = currentTime - LastUpdateTime;
                        workDoneSinceLastUpdate = timeSinceLastUpdate * buildRates[buildChunk.Identifier];
                        workDoneOnThisChunk = workDoneByLastUpdate + workDoneSinceLastUpdate;
                        chunkDone = workDoneOnThisChunk > buildChunk.Work;
                    }
                }

                if (chunkDone) workDoneOnThisChunk = buildChunk.Work;

                workDoneSinceLastUpdate = workDoneOnThisChunk - workDoneByLastUpdate;

                WorkDone += workDoneOnThisChunk;

                var timeSpentOnChunkSinceLastUpdate = buildChunk.Overhead + workDoneSinceLastUpdate / buildRates[buildChunk.Identifier];

                if (chunkDone)
                {
                    // Build chunk completed by current time

                    if (buildChunk.CompletionTime < 0)
                    {
                        buildChunk.CompletionTime = LastUpdateTime + timeSpentOnChunkSinceLastUpdate;
                    }

                    mostRecentChunkCompletetionTime = buildChunk.CompletionTime;
                    workDoneOnPreviousChunks += buildChunk.Work;

                    if (workDoneOnPreviousChunks > totalWorkDoneByLastUpdate)
                    {
                        LastUpdateTime = buildChunk.CompletionTime;
                        totalWorkDoneByLastUpdate = workDoneOnPreviousChunks;
                    }
                }
                else
                {
                    // Build chunk in progress, stop processing here
                    LastUpdateTime = currentTime;
                    break;
                }
            }

            return chunkDone;
        }
    }
}
