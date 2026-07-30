# PZ_Vanilla_Map_B42

![Kentucky in WorldEd](kentucky.jpg)

Project Zomboid Vanilla Map exported back to a WorldEd/TileZed editable project.

This export is based on Project Zomboid Build 42 compiled map files and was generated with private reverse-engineering tools.

## Export Content

- Project Zomboid Build 42 map data
- Compiled `.lotheader` and `.lotpack` files decoded
- Build 42 source cells, originally `256x256`, reprojected to WorldEd-style `300x300` TMX cells
- Variable Z levels preserved where present in the compiled data
- Tiles exported at their decoded floor, layer, and world position
- RoomDefs exported and linked from TMX files
- `objects.lua` parsed and restored into the `.pzw` project
- Objects are visible and editable in WorldEd
- Supplemental building TBX files exported in parallel for inspection/reuse

## Generated Files

- `Kentucky_full.pzw`  
  Full WorldEd project with TMX cells, RoomDefs, and objects.

- `tmx/`  
  Reconstructed `300x300` TMX cells.

- `tmx/tbx/<cell>/`  
  RoomDef TBX files referenced by the TMX files.

- `tbx_buildings/<source-cell>/`  
  Experimental building-level TBX files reconstructed from compiled building room ids.

## About The TBX Export

Each RoomDef is still exported room-by-room so WorldEd can load and display the map correctly.

In addition, this export contains a separate building TBX dump. These files are not the original source TBX files from The Indie Stone. They are reconstructed from compiled lot data by grouping rooms belonging to the same compiled building and cropping the decoded tiles around that building.

This makes it possible to inspect, sort, reuse, or package vanilla buildings separately, but it should be considered a reconstruction rather than a perfect recovery of the original building files.

Known limitations:

- Original TBX metadata and tile categories are not fully recoverable from the compiled map.
- Building TBX files are exported for inspection and reuse, not currently linked back into the TMX map.
- Some exterior context is intentionally filtered from building TBX files, such as `blends_natural_01_*` ground tiles.
- Room names, room masks, floors, walls, furniture, and decoded tile layers are preserved as far as the compiled data allows.

## Requirements

This export is intended to be used with the official Project Zomboid mapping tools:

- WorldEd
- TileZed

To properly open, edit, or compile the project, you also need the game tilesheets and tile definitions used by Project Zomboid Build 42.

Some tilesheets are packed inside the game texture packs and must be extracted before the project can render correctly in WorldEd/TileZed.

## Notes

Layer names are generated from the decoded layer order. They can be renamed in WorldEd if needed.

The map is exported from compiled game data. It is not provided by The Indie Stone and is the result of reverse engineering `.lotheader`, `.lotpack`, tiles, and map object data from Project Zomboid.

This project is intended for research, preservation, compatibility work, and modding workflows around the official mapping tools.

## Credits

Project Zomboid is developed by The Indie Stone.

This export is an unofficial community reverse-engineering project and is not affiliated with or endorsed by The Indie Stone.

