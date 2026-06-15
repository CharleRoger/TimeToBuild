using System;

namespace TimeToBuild
{
    public class LaunchScheduler
    {
        public double LaunchTime { get; private set; } = -1;
        public double LaunchTimeEarliest = -1;
        public double LaunchTimeNextMorning => Math.Ceiling((LaunchTimeEarliest - TimeToBuild.Instance.Profile.MorningTime) / TimeToBuild.Instance.Calendar.Day) * TimeToBuild.Instance.Calendar.Day + TimeToBuild.Instance.Profile.MorningTime;
        public bool LaunchScheduled => LaunchTime > 0;

        public void ScheduleLaunch(double launchTime, string vesselName)
        {
            UnscheduleLaunch();

            LaunchTime = launchTime;
        }

        public void UnscheduleLaunch()
        {
            LaunchTime = -1;
        }

        public void ResetTime()
        {
            HighLogic.CurrentGame.flightState.universalTime = TimeToBuild.Instance.Scenario.EditorStartTime;
            UnscheduleLaunch();
        }

        public void WarpToLaunchTime()
        {
            HighLogic.CurrentGame.flightState.universalTime = LaunchTime;
            UnscheduleLaunch();
        }
    }
}