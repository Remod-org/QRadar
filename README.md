# QRadar

Simple player radar for world objects:

When the player types the command, the plugin scans the area for entities within a specified range.  It then places on-screen text at the location of those entities, color-coded by type.  This stays on the screen for a specified number of seconds.

### Command

 - /qradar -- Begin search from player location

 This may be one of those commands you will want to bind to a key.  Open the F1 console and type, e.g.:

  - bind y qradar

### Permission

 - qradar.use -- Allow running the command above

### Configuration
```json
{
  "playSound": true,
  "range": 50.0,
  "duration": 10.0,
  "frequency": 20.0,
  "showPlayersForAdmin" : true,
  "showPlayersForAll": false,
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 2
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

