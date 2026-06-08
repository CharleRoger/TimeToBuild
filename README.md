# Time To Build

Time To Build is a mod for Kerbal Space Program which primarily simulates in-game time taken to develop, construct, refurbish, and rollout vessels.

This mod is designed to provide a lightweight, flexible framework to implement basically any construction-time system you can think of while seamlessly and intuitively integrating with stock game mechanics.

## Features

### Current features

#### Core functionality

When launching a vessel from the editor, a dialog pops up:

- Lists a breakdown of time required to ready the vessel for launch.
- Current date, completion date, intermediate alarms and contract deadlines are also shown.
- "Warp to earliest launch" or "Warp to next morning" to immediately time warp and seamlessly launch as normal.
- "Start build" to begin constructing the vessel in the background.
  - Currently, each of the VAB and SPH can only construct one vessel at a time.
  - An alarm is added at the estimated completion date.
  - Upon completion, another dialog pops up wherever you are with the same launch options as above.

Reverting to the VAB or SPH reverts back to before the construction of the vessel.

The ability to launch from the launchpad and runway facilities is currently disabled. These facilities are planned to take on new functionality in the future (see planned features below).

#### Configuration

The default configuration defines three periods of time to take a vessel from parts, assumed to have already been designed and fabricated elsewhere, to launch:

- **Development**: Represents time taken to test new parts and design vessels and processes, derived from the cost of individual parts. Development time decreases a bit as the VAB or SPH is upgraded, but is generally high for new and/or expensive parts and rapidly approaches zero as parts are repeatedly rebuilt, regardless of the design of the actual vessel itself. Recovered and reused parts have zero development time.
- **Assembly**: Represents time taken to assemble the final vessel once the development is completed, largely based on the dry mass of individual new parts combined with a small contribution from cost. Assembly time decreases significantly as the relevant assembly building is upgraded. Recovered and reused parts have zero assembly time.
- **Refurbishment**: Represents time taken to refurbish recovered and reused parts, currently set to one sixth of the assembly time.
- **Rollout**: Represents time taken to physically move the vessel to the launch facility and prepare it for launch. This is dependent on the wet mass of the vessel at launch.

Custom configurations can be described via a `TimeToBuildConfig` node with any number of `BuildTime` and `ResearchTime` nodes as well as the following fields:
- `MorningTime` in seconds, for warp-to-launch-next-morning functionality.
- `AlarmWarningBufferTime` in seconds, for listing alarms in the in-game TimeToBuild dialog which are set after the completion time, but within the specified buffer period.

`BuildTime` nodes control time taken to construct and rollout vessels and take the following fields:
- `name`, an arbitrary string which should ideally be unique.
- `Title`, displayed in the build-time dialog in game.
- Any number of `Facility`s in which to operate, e.g. on construction of a vessel in the VAB/SPH and/or rollout of the vessel onto the launchpad/runway: `Launchpad`, `Runway`, `VehicleAssemblyBuilding`, `SpaceplaneHangar`.
- `WholeVessel = true` or `PerNewPart = true` and/or `PerReusedPart = true`, specifies whether the formula applies to the whole vessel or as a sum over new or recovered parts.

`ResearchTime` nodes control time taken to unlock tech tree nodes and take the following fields:
- `name`, an arbitrary string which should ideally be unique.

Each `BuildTime` and `ResearchTime` node must define one `TimeFormula` node which itself has three fields, `Work`, `Rate`, and `Overhead`, with the total time computed as `Work / Rate + Overhead`. Each of the three fields is an arbitrary mathematical formula with the following constraints:
  - Supports all the basic mathematical operations `+ - * / ^ ( )`.
  - The following variables are currently recognised:
    - Time units: `year`, `day` (of the home planet), `hour`, `minute`, `second`.
    - Facility levels: `facility_level` (current assembly/launch facility), `Administration_level`, `AstronautComplex_level`, `Launchpad_level`, `MissionControl_level`, `ResearchAndDevelopment_level`, `Runway_level`, `SpaceplaneHangar_level`, `TrackingStation_level`, `VehicleAssemblyBuilding_level`.
    - Whole-vessel properties: `dry_cost`, `dry_mass`, `wet_mass`, `wet_cost`, `num_parts`.
    - Individual part properties: `num_builds` (tracked by ScrapYard), `dry_cost`, `dry_mass`, `wet_mass`, `wet_cost`.

### Planned features

#### Tech tree research time

- Tech tree unlocks can also be configured to take time with a custom formula.
  - Individual tech tree nodes can be configured to override the global formula.

#### Vessel storage

Vessels built in the background are added to storage upon completion rather than being rolled out for launch.
- Each of the VAB and SPH can store a limited number of completed vessels, subject to facility level.

The launchpad and runway now display stored vessels rather than all vessel designs.
- Vessels under construction are listed but cannot be launched yet.
- A completed vessel in storage can be rolled out and launched as normal with selected crew where applicable.
- A completed vessel can be moved back to assembly where it is converted into ScrapYard inventory parts.

#### Recovery and reuse

Recovered vessels are added to the vessel storage, bypassing the stock parts-for-cash system. After a refurbishment period and cost, they become available to rollout like a newly constructed vehicle. 

#### Additional build rates

Facility upgrades unlock additional "team" slots. Multiple teams can be assigned to a task to speed it up, or used for parallel construction.

## Dependencies

- [ScrapYard (2.2.99.1)](https://github.com/zer0Kerbal/ScrapYard)
- [BackgroundResourceProcessing (0.3.0)](https://github.com/Phantomical/BackgroundResourceProcessing) (Not a hard dependency, but essentially required for expected simulation behaviour)

## Compatibility

This mod is still in the pre-release phase and has not been thoroughly tested for compatibility with other mods. You can expect it to clash with other mods which similarly meddle with vessel construction/instantiation.

## License

Distributed under [the MIT license](https://opensource.org/license/mit).