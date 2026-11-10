using Pakar.Domain.Services;

namespace Pakar.Api.Tests;

public class GeoDistanceTests
{
    [Fact]
    public void Same_point_is_zero_meters()
    {
        var point = new GeoPoint(-7.7685, 110.3765);
        Assert.Equal(0, GeoDistance.HaversineMeters(point, point), precision: 6);
    }

    [Fact]
    public void One_degree_of_latitude_is_about_111_km()
    {
        var distance = GeoDistance.HaversineMeters(new GeoPoint(0, 0), new GeoPoint(1, 0));
        Assert.InRange(distance, 111_100, 111_300);
    }

    [Fact]
    public void Campus_distance_matches_known_scale()
    {
        // 0.0005 degrees of latitude near Yogyakarta is about 55.6 m.
        var distance = GeoDistance.HaversineMeters(new GeoPoint(-7.7685, 110.3765), new GeoPoint(-7.7690, 110.3765));
        Assert.InRange(distance, 55, 56.5);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var a = new GeoPoint(-7.7685, 110.3765);
        var b = new GeoPoint(-7.7701, 110.3741);
        Assert.Equal(GeoDistance.HaversineMeters(a, b), GeoDistance.HaversineMeters(b, a), precision: 6);
    }

    [Theory]
    [InlineData(-7.77, 110.37, true)]
    [InlineData(91, 0, false)]
    [InlineData(0, -181, false)]
    [InlineData(double.NaN, 0, false)]
    public void Coordinates_are_validated(double lat, double lng, bool expected)
    {
        Assert.Equal(expected, GeoPoint.IsValid(lat, lng));
    }
}
