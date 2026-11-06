using Heimevernet_Prosjekt.Services;
using Xunit;

namespace Heimevernet_Prosjekt.Tests.Services.tests;

public sealed class GeoDistanceTests
{
    [Fact]
    public void Km_WhenPointsAreTheSame_ReturnsZero()
    {
        var distance = GeoDistance.Km(59.9139, 10.7522, 59.9139, 10.7522);

        Assert.Equal(0, distance, precision: 10);
    }

    [Fact]
    public void Km_IsSymmetric()
    {
        var firstToSecond = GeoDistance.Km(59.9139, 10.7522, 60.3913, 5.3221);
        var secondToFirst = GeoDistance.Km(60.3913, 5.3221, 59.9139, 10.7522);

        Assert.Equal(firstToSecond, secondToFirst, precision: 10);
    }

    [Fact]
    public void Km_ReturnsExpectedDistanceBetweenOsloAndBergen()
    {
        var distance = GeoDistance.Km(59.9139, 10.7522, 60.3913, 5.3221);

        Assert.InRange(distance, 305, 306);
    }

    [Fact]
    public void Km_ReturnsHalfEarthCircumferenceForAntipodalPoints()
    {
        var distance = GeoDistance.Km(0, 0, 0, 180);

        Assert.InRange(distance, 20000, 20040);
    }
}
