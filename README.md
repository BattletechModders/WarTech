# WarTech
BattleTech mod (using ModTek), that enables factions on the map to war each other.

## Requirements
* install [BattleTechModLoader](https://github.com/Mpstark/BattleTechModLoader/releases) using the [instructions here](https://github.com/Mpstark/BattleTechModLoader)
* install [ModTek](https://github.com/Mpstark/ModTek/releases) using the [instructions here](https://github.com/Mpstark/ModTek)

## Recommended
* [InnerSphereMap](https://www.nexusmods.com/battletech/mods/148)

## Features
- Factions start, end and join wars
- Factions attack system they see as valuable
- Doing a mission for one faction raises control for this faction
- Planets change owners if control percentage is flipped
- Border planets have higher difficulty
- Contract targets depend on neighbour planets
- If you are liked by a Faction they offer you good paying priority contracts for target planets

## Download
Downloads can be found on [github](https://github.com/Morphyum/WarTech/releases).

## Settings
Setting | Type | Default | Description
--- | --- | --- | ---
AttackPercentagePerPlayerMission | int | default 10 | How much control a succefull player mission will grant the employee faction.
excludedFactionNames | List<string>| default ["MercenaryReviewBoard","ComStar","Locals","AuriganPirates","NoFaction"] | Factions listed here will be excluded from the wars.
PercentageForControl | int | default 30 | How much percentage of control is needed at minimum to control a planet.
WeightOfNeighbours | int | default 1 | How important it is for Factions to conquer planets that have lots of own neighbours(prevents border gore)
minMonthDuration | int | default 1 | The minimal Number of month a war goes before factions have achance to surrender.
maxMonthDuration | int | default -1 | The maximal Number of month a war goes before it ends automaticly (-1 = infinite)
InlandPlanetDifficulty | int | default 0 | The Planet difficulty of inland planets.
BorderPlanetDifficulty | int | default 0 | The Planet difficulty of border planets.
priorityContactPayPercentage | float | default 2 | The multiplicator for the priority missions (normal payment * priorityContactPayPercentage )(normal salvage * priorityContactPayPercentage)
FatiquePerLostAttack | float | default 0.5 | How much war exhaution each faction gets for a lost background battle.
FatiqueLostPerMonth | float | default 3 | How much war exhaution each faction gets each month at war.
FatiqueRecoveredPerDay | float | default 0.5 | How much war exhaution each faction recovers each day when out of war.
FatiquePerPlanetCapture | float | default 3 | How much war exhaution each faction recovers and the other faction loses when it conqueres a new planet.
attackercolor | string| default "#ff0000ff" | Text color for attacking factions in monthly report.
defendercolor | string| default "#008000ff" | Text color for defending factions in monthly report.
planetcolor | string| default "#00ffffff" | Text color for planets in monthly report.
    
## Install
- After installing BTML and ModTek, put  everything into \BATTLETECH\Mods\ folder.
- If you want different settings set it in the settings.json.
- Start the game.
