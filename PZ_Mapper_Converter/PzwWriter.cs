using System.Text;

namespace PZ_Mapper_Converter;

internal static class PzwWriter
{
    private static readonly string[] KnownObjectTypes =
    {
        "TownZone",
        "Forest",
        "DeepForest",
        "Nav",
        "Vegitation",
        "TrailerPark",
        "Farm",
        "ParkingStall",
        "FarmLand",
        "WaterFlow",
        "WaterZone",
        "Mannequin",
        "Ranch",
        "ZombiesType",
        "LootZone",
        "ZoneStory",
        "SpawnPoint",
        "RoomTone",
        "Basement",
        "WorldGen",
        "AnimalZone"
    };

    private static readonly Dictionary<string, string> ObjectGroupColors = new(StringComparer.Ordinal)
    {
        ["ParkingStall"] = "#ff007f",
        ["TownZone"] = "#aa0000",
        ["Forest"] = "#00aa00",
        ["Nav"] = "#55aaff",
        ["DeepForest"] = "#003500",
        ["Vegitation"] = "#b3b300",
        ["TrailerPark"] = "#f50000",
        ["Farm"] = "#55ff7f",
        ["FarmLand"] = "#bcff7d",
        ["WaterFlow"] = "#0000ff",
        ["WaterZone"] = "#0000ff",
        ["Mannequin"] = "#0000ff",
        ["Ranch"] = "#ff8000",
        ["ZombiesType"] = "#ffffff",
        ["LootZone"] = "#80ff80",
        ["ZoneStory"] = "#8080ff",
        ["SpawnPoint"] = "#101010",
        ["RoomTone"] = "#0000ff",
        ["Basement"] = "#8040ff",
        ["WorldGen"] = "#808080",
        ["AnimalZone"] = "#ffaa00"
    };

    private static readonly HashSet<string> BuiltInPropertyDefs = new(StringComparer.Ordinal)
    {
        "Direction",
        "FaceDirection",
        "WaterDirection",
        "WaterSpeed",
        "WaterGround",
        "WaterShore",
        "Female",
        "Pose",
        "Skin",
        "Professions",
        "Outfit",
        "Script",
        "RoomTone",
        "EntireBuilding",
        "StairDirection",
        "StairX",
        "StairY",
        "Access",
        "Rocks",
        "Action"
    };

    public static void Write(
        string file,
        string projectName,
        IEnumerable<CellCoord> cells,
        IEnumerable<MapObject> objects,
        int targetCellSize)
    {
        var cellList = cells.OrderBy(c => c.X).ThenBy(c => c.Y).ToArray();
        if (cellList.Length == 0)
        {
            return;
        }

        var objectList = objects
            .Where(obj => obj.CanWrite)
            .Where(obj => !string.IsNullOrWhiteSpace(obj.Type))
            .ToArray();

        var objectsByCell = objectList
            .GroupBy(obj => obj.GetCell(targetCellSize))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(AnchorX)
                    .ThenBy(AnchorY)
                    .ThenBy(obj => obj.Type, StringComparer.Ordinal)
                    .ToArray());

        var minX = cellList.Min(c => c.X);
        var minY = cellList.Min(c => c.Y);
        var maxX = cellList.Max(c => c.X);
        var maxY = cellList.Max(c => c.Y);
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;

        using var writer = new StreamWriter(file, false, Encoding.UTF8);
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        writer.WriteLine($"<world version=\"1.0\" width=\"{width}\" height=\"{height}\">");
        writer.WriteLine("<BMPToTMX>");
        writer.WriteLine("<tmxexportdir path=\"tmx\"/>");
        writer.WriteLine("<rulesfile path=\"\"/>");
        writer.WriteLine("<blendsfile path=\"\"/>");
        writer.WriteLine("<mapbasefile path=\"\"/>");
        writer.WriteLine("<assign-maps-to-world checked=\"false\"/>");
        writer.WriteLine("<warn-unknown-colors checked=\"true\"/>");
        writer.WriteLine("<compress checked=\"true\"/>");
        writer.WriteLine("<copy-pixels checked=\"true\"/>");
        writer.WriteLine("<update-existing checked=\"true\"/>");
        writer.WriteLine("</BMPToTMX>");
        writer.WriteLine("<TMXToBMP>");
        writer.WriteLine("<mainImage generate=\"true\"/>");
        writer.WriteLine("<vegetationImage generate=\"true\"/>");
        writer.WriteLine("<buildingsImage path=\"\" generate=\"false\"/>");
        writer.WriteLine("</TMXToBMP>");
        writer.WriteLine("<GenerateLots>");
        writer.WriteLine("<exportdir path=\"lots\"/>");
        writer.WriteLine("<ZombieSpawnMap path=\"Map_ZombieSpawnMap.png\"/>");
        writer.WriteLine("<TileDefFolder path=\"\"/>");
        writer.WriteLine($"<worldOrigin origin=\"{minX},{minY}\"/>");
        writer.WriteLine("</GenerateLots>");
        writer.WriteLine("<LuaSettings>");
        writer.WriteLine("<spawnPointsFile path=\"spawnpoints.lua\"/>");
        writer.WriteLine("<worldObjectsFile path=\"objects.lua\"/>");
        writer.WriteLine("</LuaSettings>");
        writer.WriteLine("<otherworld path=\"\"/>");
        WriteCommonTypes(writer, objectList);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var coord = new CellCoord(x, y);
                writer.WriteLine($"<cell x=\"{x - minX}\" y=\"{y - minY}\" map=\"tmx/{x}_{y}.tmx\">");

                if (objectsByCell.TryGetValue(coord, out var cellObjects))
                {
                    foreach (var mapObject in cellObjects)
                    {
                        WriteObject(writer, mapObject, coord, targetCellSize);
                    }
                }

                writer.WriteLine("</cell>");
            }
        }

        writer.WriteLine("</world>");
    }

    private static void WriteCommonTypes(StreamWriter writer, IReadOnlyList<MapObject> objects)
    {
        writer.WriteLine("<propertyenum name=\"Direction\" choices=\"N,S,W,E\" multi=\"false\"/>");
        writer.WriteLine("<propertyenum name=\"Pose\" choices=\"pose01,pose02,pose03\" multi=\"false\"/>");
        writer.WriteLine("<propertyenum name=\"Skin\" choices=\"White,Black\" multi=\"false\"/>");
        writer.WriteLine("<propertyenum name=\"Professions\" choices=\"unemployed,chef,constructionworker,doctor,fireofficer,parkranger,policeofficer,repairman,securityguard\" multi=\"false\"/>");
        writer.WriteLine("<propertyenum name=\"RoomTone\" choices=\"Generic,Barn,Mall,Warehouse,Prison,Church,Office,Factory\" multi=\"false\"/>");
        writer.WriteLine("<propertydef name=\"Direction\" default=\"N\" enum=\"Direction\"/>");
        writer.WriteLine("<propertydef name=\"FaceDirection\" default=\"true\"/>");
        writer.WriteLine("<propertydef name=\"WaterDirection\" default=\"0.0\"/>");
        writer.WriteLine("<propertydef name=\"WaterSpeed\" default=\"0.0\"/>");
        writer.WriteLine("<propertydef name=\"WaterGround\" default=\"false\"/>");
        writer.WriteLine("<propertydef name=\"WaterShore\" default=\"true\"/>");
        writer.WriteLine("<propertydef name=\"Female\" default=\"true\"/>");
        writer.WriteLine("<propertydef name=\"Pose\" default=\"pose01\" enum=\"Pose\"/>");
        writer.WriteLine("<propertydef name=\"Skin\" default=\"White\" enum=\"Skin\"/>");
        writer.WriteLine("<propertydef name=\"Professions\" default=\"all\" enum=\"Professions\"/>");
        writer.WriteLine("<propertydef name=\"Outfit\" default=\"\"/>");
        writer.WriteLine("<propertydef name=\"Script\" default=\"\"/>");
        writer.WriteLine("<propertydef name=\"RoomTone\" default=\"Generic\" enum=\"RoomTone\"/>");
        writer.WriteLine("<propertydef name=\"EntireBuilding\" default=\"false\"/>");
        writer.WriteLine("<propertydef name=\"StairDirection\" default=\"N\" enum=\"Direction\"/>");
        writer.WriteLine("<propertydef name=\"StairX\" default=\"0\"/>");
        writer.WriteLine("<propertydef name=\"StairY\" default=\"0\"/>");
        writer.WriteLine("<propertydef name=\"Access\" default=\"\"/>");
        writer.WriteLine("<propertydef name=\"Rocks\" default=\"\"/>");
        writer.WriteLine("<propertydef name=\"Action\" default=\"\"/>");

        foreach (var propertyName in objects
                     .SelectMany(obj => obj.Properties.Keys)
                     .Where(name => !BuiltInPropertyDefs.Contains(name))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            writer.WriteLine($"<propertydef name=\"{Escape(propertyName)}\" default=\"\"/>");
        }

        WriteTemplates(writer);

        var types = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in KnownObjectTypes)
        {
            AddType(type);
        }

        foreach (var type in objects
                     .Select(obj => obj.Type)
                     .Where(type => !string.IsNullOrWhiteSpace(type))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(type => type, StringComparer.Ordinal))
        {
            AddType(type);
        }

        foreach (var type in types)
        {
            writer.WriteLine($"<objecttype name=\"{Escape(type)}\"/>");
        }

        foreach (var type in types)
        {
            var color = ObjectGroupColors.GetValueOrDefault(type, "#808080");
            writer.WriteLine($"<objectgroup name=\"{Escape(type)}\" color=\"{color}\" defaulttype=\"{Escape(type)}\"/>");
        }

        void AddType(string type)
        {
            if (!string.IsNullOrWhiteSpace(type) && seen.Add(type))
            {
                types.Add(type);
            }
        }
    }

    private static void WriteTemplates(StreamWriter writer)
    {
        writer.WriteLine("<template name=\"ParkingStallN\">");
        writer.WriteLine("<property name=\"Direction\" value=\"N\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"ParkingStallS\">");
        writer.WriteLine("<property name=\"Direction\" value=\"S\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"ParkingStallW\">");
        writer.WriteLine("<property name=\"Direction\" value=\"W\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"ParkingStallE\">");
        writer.WriteLine("<property name=\"Direction\" value=\"E\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"WaterFlowN\">");
        writer.WriteLine("<property name=\"WaterDirection\" value=\"0\"/>");
        writer.WriteLine("<property name=\"WaterSpeed\" value=\"1.0\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"WaterFlowS\">");
        writer.WriteLine("<property name=\"WaterDirection\" value=\"180\"/>");
        writer.WriteLine("<property name=\"WaterSpeed\" value=\"1.0\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"WaterFlowE\">");
        writer.WriteLine("<property name=\"WaterDirection\" value=\"90\"/>");
        writer.WriteLine("<property name=\"WaterSpeed\" value=\"1.0\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"WaterFlowW\">");
        writer.WriteLine("<property name=\"WaterDirection\" value=\"270\"/>");
        writer.WriteLine("<property name=\"WaterSpeed\" value=\"1.0\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"WaterZone\">");
        writer.WriteLine("<property name=\"WaterGround\" value=\"false\"/>");
        writer.WriteLine("<property name=\"WaterShore\" value=\"true\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"SpawnPoint\">");
        writer.WriteLine("<description></description>");
        writer.WriteLine("<property name=\"Professions\" value=\"all\"/>");
        writer.WriteLine("</template>");
        writer.WriteLine("<template name=\"RoomTone\">");
        writer.WriteLine("<property name=\"RoomTone\" value=\"Generic\"/>");
        writer.WriteLine("<property name=\"EntireBuilding\" value=\"false\"/>");
        writer.WriteLine("</template>");
    }

    private static void WriteObject(StreamWriter writer, MapObject mapObject, CellCoord cell, int targetCellSize)
    {
        if (mapObject.IsGeometry)
        {
            WriteGeometryObject(writer, mapObject, cell, targetCellSize);
        }
        else
        {
            WriteRectangleObject(writer, mapObject, cell, targetCellSize);
        }
    }

    private static void WriteRectangleObject(StreamWriter writer, MapObject mapObject, CellCoord cell, int targetCellSize)
    {
        if (!mapObject.X.HasValue || !mapObject.Y.HasValue)
        {
            return;
        }

        var localX = mapObject.X.Value - cell.X * targetCellSize;
        var localY = mapObject.Y.Value - cell.Y * targetCellSize;
        var attributes = new List<string>
        {
            Attribute("name", mapObject.Name),
            Attribute("group", mapObject.Type),
            Attribute("type", mapObject.Type),
            Attribute("x", localX.ToString()),
            Attribute("y", localY.ToString()),
            Attribute("level", mapObject.Level.ToString()),
            Attribute("width", (mapObject.Width ?? 0).ToString()),
            Attribute("height", (mapObject.Height ?? 0).ToString())
        };

        WriteObjectElement(writer, attributes, mapObject.Properties);
    }

    private static void WriteGeometryObject(StreamWriter writer, MapObject mapObject, CellCoord cell, int targetCellSize)
    {
        var cellMinX = cell.X * targetCellSize;
        var cellMinY = cell.Y * targetCellSize;
        var geometry = string.IsNullOrWhiteSpace(mapObject.Geometry) ? "polygon" : mapObject.Geometry.Trim();
        var points = string.Join(
            " ",
            mapObject.Points.Select(point => $"{point.X - cellMinX},{point.Y - cellMinY}"));

        var attributes = new List<string>
        {
            Attribute("name", mapObject.Name),
            Attribute("group", mapObject.Type),
            Attribute("type", mapObject.Type),
            Attribute("level", mapObject.Level.ToString()),
            Attribute("geometry", geometry),
            Attribute("points", points)
        };

        if (geometry.Equals("polyline", StringComparison.OrdinalIgnoreCase) && mapObject.LineWidth.HasValue)
        {
            attributes.Add(Attribute("linewidth", mapObject.LineWidth.Value.ToString()));
        }

        WriteObjectElement(writer, attributes, mapObject.Properties);
    }

    private static void WriteObjectElement(StreamWriter writer, IReadOnlyList<string> attributes, IReadOnlyDictionary<string, string> properties)
    {
        var attributeText = string.Join(" ", attributes);
        if (properties.Count == 0)
        {
            writer.WriteLine($"  <object {attributeText}/>");
            return;
        }

        writer.WriteLine($"  <object {attributeText}>");
        foreach (var property in properties)
        {
            writer.WriteLine($"   <property name=\"{Escape(property.Key)}\" value=\"{Escape(property.Value)}\"/>");
        }

        writer.WriteLine("  </object>");
    }

    private static string Attribute(string name, string value) => $"{name}=\"{Escape(value)}\"";

    private static int AnchorX(MapObject mapObject) => mapObject.IsGeometry ? mapObject.Points[0].X : mapObject.X ?? 0;

    private static int AnchorY(MapObject mapObject) => mapObject.IsGeometry ? mapObject.Points[0].Y : mapObject.Y ?? 0;

    private static string Escape(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
