# 0.2.1
- Fixed "Warp to earliest launch" and "Warp to next morning" buttons
# 0.2.0
- Split `BuildTime` `Formula` into `WorkFormula`, `RateFormula` and `OverheadFormula`, where the total time is computed as `Work / Rate + Overhead`.
- Added "Start build" option to editor build dialog:
  - Vessel build progress continues in the background.
  - Currently, each of the VAB and SPH can only construct one vessel at a time.
  - An alarm is added at the estimated completion date.
  - Upon completion, another dialog pops up wherever you are with a button to launch to vessel.
# 0.1.0
- Initial pre-release.