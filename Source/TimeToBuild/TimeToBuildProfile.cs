using System;
using System.Collections.Generic;

namespace TimeToBuild
{
    public class TimeToBuildProfile
    {
        public string Name { get; private set; } = "";
        public string Title { get; private set; } = "";
        public string Description { get; private set; } = "";
        public double MorningTime { get; private set; } = 0;
        public double AlarmWarningBufferTime { get; private set; } = 0;
        public Dictionary<string, BuildTime> BuildTimes { get; private set; } = new Dictionary<string, BuildTime>();

        public TimeToBuildProfile(ConfigNode node)
        {
            if (node.HasValue("name")) Name = node.GetValue("name");
            if (node.HasValue("title")) Title = node.GetValue("title");
            if (node.HasValue("description")) Description = node.GetValue("description");
            if (node.HasValue("MorningTime")) MorningTime = double.Parse(node.GetValue("MorningTime"));
            if (node.HasValue("AlarmWarningBufferTime")) MorningTime = double.Parse(node.GetValue("AlarmWarningBufferTime"));
            foreach (var buildTimeNode in node.GetNodes("BuildTime"))
            {
                var buildTime = new BuildTime(buildTimeNode);
                if (buildTime.Name != "") BuildTimes[buildTime.Name] = buildTime;
            }
        }

        public static TimeToBuildProfile GetTimeToBuildProfile(string name)
        {
            var allTimeToBuildProfileNodes = GameDatabase.Instance.GetConfigNodes("TimeToBuildProfile");
            var allTimeToBuildProfiles = new Dictionary<string, TimeToBuildProfile>();
            foreach (var timeToBuildProfileNode in allTimeToBuildProfileNodes)
            {
                if (timeToBuildProfileNode.HasValue("name") && timeToBuildProfileNode.GetValue("name") == name)
                {
                    return new TimeToBuildProfile(timeToBuildProfileNode);
                }
            }
            return null;
        }
    }
}