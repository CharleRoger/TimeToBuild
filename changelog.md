# 0.3.0
- Changed `BuildTime` formulae to be configured as a single `TimeFormula` node with three fields: `Work`, `Rate` and `Overhead`.
- Added the ability to define `ResearchTime` nodes in a `TimeToBuildProfile` which control time taken to unlock tech tree nodes.
  - In the default configuration, research takes 60*sqrt(cost/5) days.
# 0.2.2
- Added "Warp to next morning" button to build-completion launch dialog.
# 0.2.1
- Fixed "Warp to earliest launch" and "Warp to next morning" buttons.
- Re-enabled editor and launchpad/runway launch site selector (note that launching from the launchpad/runway is still disabled).
# 0.2.0
- Split `BuildTime` `Formula` into `WorkFormula`, `RateFormula` and `OverheadFormula`, where the total time is computed as `Work / Rate + Overhead`.
- Added "Start build" option to editor build dialog:
  - Vessel build progress continues in the background.
  - Currently, each of the VAB and SPH can only construct one vessel at a time.
  - An alarm is added at the estimated completion date.
  - Upon completion, another dialog pops up wherever you are with a button to launch to vessel.
# 0.1.0
- Initial pre-release.