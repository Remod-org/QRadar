# QRadar

Simple player radar for world objects

When the player types the command, the plugin scans the area for entities within a specified range.  It then places on-screen text at the location of those entities, color-coded by type.  This stays on the screen for a specified number of seconds.

If the player also has the qradar.held permission, they can spawn a geiger counter.

 - Once held, they can use the main FIRE button to perform the same scan as with the /qradar command.
 - They can also use the FIRE2 button to describe an item in front of them.

 - If "specialHandlingForGC" is true (default):
   - They can move the geiger counter (or any geiger counter) between their belt and main inventory, or to and from a backpack (using the Backpacks plugin).
   - If they drop the geiger counter, it will disappear.
   - When they disconnect, the geiger counter will be removed from their inventory or backpack.

### Command

 - /qradar -- Begin search from player location

 This may be one of those commands you will want to bind to a key.  Open the F1 console and type, e.g.:

  - bind y qradar

 - /qcounter -- Spawn a geiger counter to use for also describing viewed items

### Permission

 - qradar.use -- Allow running the the /qradar command
 - qradar.held -- Allow spawning of a geiger counter using /qcounter (limit of one)

### Configuration
```json
{
  "playSound": true,
  "range": 50.0,
  "duration": 10.0,
  "frequency": 20.0,
  "showPlayersForAdmin" : true,
  "showPlayersForAll": false,
  "specialHandlingForGC": true,
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 3
  }
}
```

 - playSound -- play a sound when the command is run (or bound key)
 - range -- How far out should the tool look for entities
 - duration -- How long should the text remain on screen
 - frequency -- How often are players allowed to use the command
 - showPlayersForAdmin -- Admin can also see other players / scientists
 - showPlayersForAll -- Players can also see other players / scientists

### NOTES

Currently, the plugin will not display items within range of a tool cupboard.  The goal here is to find resources and players, not scope out the inside of someone's home.

The special handling of geiger counters is mainly set to prevent abuse (infinite respawning to gain recycleable materials, etc.).

