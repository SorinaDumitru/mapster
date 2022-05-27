using Mapster.Common.MemoryMappedTypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mapster.Rendering;

public static class TileRenderer
{
    public static BaseShape Tessellate(this MapFeatureData feature, ref BoundingBox boundingBox, ref PriorityQueue<BaseShape, int> shapes)
    {
        BaseShape? baseShape = null;

        var featureType = feature.Type;
        var isBorder = false;
        var isPopPlace = false;
        ReadOnlySpan<Coordinate> coordinates;
        GeoFeature geoFeature;
        foreach (var property in feature.Properties)
        {
            switch (property)
            {   
                case var _ when (property >= (short)MapProperties.HMOTORWAY && property <= (short)MapProperties.HROAD):
                    coordinates = feature.Coordinates;
                    var road = new Road(coordinates);
                    baseShape = road;
                    shapes.Enqueue(road, road.ZIndex);
                    break;
                case var _ when (property.Equals((short)MapProperties.WATER) && featureType != GeometryType.Point):
                    coordinates = feature.Coordinates;
                    var waterway = new Waterway(coordinates, feature.Type == GeometryType.Polygon);
                    baseShape = waterway;
                    shapes.Enqueue(waterway, waterway.ZIndex);
                    break;
                case var _ when (!isBorder && Border.ShouldBeBorder(feature)):
                    coordinates = feature.Coordinates;
                    var border = new Border(coordinates);
                    baseShape = border;
                    shapes.Enqueue(border, border.ZIndex);
                    isBorder = true;
                    break;
                case var _ when (!isPopPlace && PopulatedPlace.ShouldBePopulatedPlace(feature, property)):
                    coordinates = feature.Coordinates;
                    var popPlace = new PopulatedPlace(coordinates, feature);
                    baseShape = popPlace;
                    shapes.Enqueue(popPlace, popPlace.ZIndex);
                    isPopPlace = true;
                    break;
                case var _ when (property.Equals((short)MapProperties.RAILWAY)):
                    coordinates = feature.Coordinates;
                    var railway = new Railway(coordinates);
                    baseShape = railway;
                    shapes.Enqueue(railway, railway.ZIndex);
                    break;
                case var _ when (property >= (short)MapProperties.NFELL && property <= (short)MapProperties.NWATER && featureType == GeometryType.Polygon):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, feature, property);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (property.Equals((short)MapProperties.BFOREST)):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Forest);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (property.Equals((short)MapProperties.LFOREST) || property.Equals((short)MapProperties.LORCHARD)):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Forest);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (feature.Type == GeometryType.Polygon && property >= (short)MapProperties.LRESIDENTIAL && property <= (short)MapProperties.LBROWNFIELD):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex); 
                    break;
                case var _ when (feature.Type == GeometryType.Polygon && property >= (short)MapProperties.LFARM && property <= (short)MapProperties.LALOTTMENTS):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Plain);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (feature.Type == GeometryType.Polygon && property.Equals((short)MapProperties.LRESERVOIR) ||
                     property.Equals((short)MapProperties.LBASIN)):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Water);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (feature.Type == GeometryType.Polygon && property.Equals((short)MapProperties.BUILDING)):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (feature.Type == GeometryType.Polygon && property.Equals((short)MapProperties.LEISURE)):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                case var _ when (feature.Type == GeometryType.Polygon && property.Equals((short)MapProperties.AMENITY)):
                    coordinates = feature.Coordinates;
                    geoFeature = new GeoFeature(coordinates, GeoFeature.GeoFeatureType.Residential);
                    baseShape = geoFeature;
                    shapes.Enqueue(geoFeature, geoFeature.ZIndex);
                    break;
                default:
                    break;
            }
        }

        if (baseShape != null)
        {
            for (var j = 0; j < baseShape.ScreenCoordinates.Length; ++j)
            {
                boundingBox.MinX = Math.Min(boundingBox.MinX, baseShape.ScreenCoordinates[j].X);
                boundingBox.MaxX = Math.Max(boundingBox.MaxX, baseShape.ScreenCoordinates[j].X);
                boundingBox.MinY = Math.Min(boundingBox.MinY, baseShape.ScreenCoordinates[j].Y);
                boundingBox.MaxY = Math.Max(boundingBox.MaxY, baseShape.ScreenCoordinates[j].Y);
            }
        }

        return baseShape;
    }

    public static Image<Rgba32> Render(this PriorityQueue<BaseShape, int> shapes, BoundingBox boundingBox, int width, int height)
    {
        var canvas = new Image<Rgba32>(width, height);

        // Calculate the scale for each pixel, essentially applying a normalization
        var scaleX = canvas.Width / (boundingBox.MaxX - boundingBox.MinX);
        var scaleY = canvas.Height / (boundingBox.MaxY - boundingBox.MinY);
        var scale = Math.Min(scaleX, scaleY);

        // Background Fill
        canvas.Mutate(x => x.Fill(Color.White));
        while (shapes.Count > 0)
        {
            var entry = shapes.Dequeue();
            entry.TranslateAndScale(boundingBox.MinX, boundingBox.MinY, scale, canvas.Height);
            canvas.Mutate(x => entry.Render(x));
        }

        return canvas;
    }

    public struct BoundingBox
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
    }
}
