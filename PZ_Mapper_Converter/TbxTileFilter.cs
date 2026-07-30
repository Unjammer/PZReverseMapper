namespace PZ_Mapper_Converter;

internal static class TbxTileFilter
{
    public static bool ShouldInclude(string tileName)
    {
        return !tileName.Equals("blends_natural_01", StringComparison.OrdinalIgnoreCase)
            && !tileName.StartsWith("blends_natural_01_", StringComparison.OrdinalIgnoreCase)
            && !tileName.StartsWith("vegetation_trees_01", StringComparison.OrdinalIgnoreCase)
            && !tileName.StartsWith("vegetation_foliage_01", StringComparison.OrdinalIgnoreCase)
            && !tileName.StartsWith("vegetation_groundcover_01", StringComparison.OrdinalIgnoreCase)
            && !tileName.StartsWith("d_plants_01", StringComparison.OrdinalIgnoreCase)
            && !tileName.StartsWith("blends_grassoverlays_01", StringComparison.OrdinalIgnoreCase);
    }
}
