namespace Heimevernet_Prosjekt.Services
{
    // Distance in km between two points on the earth (Haversine formula) (help with AI)
    // Same calculation as DistanceKm in ResourceController, shared so needs can use it too
    public static class GeoDistance
    {
        private const double EarthRadiusKm = 6371;

        public static double Km(double lat1, double lng1, double lat2, double lng2)
        {
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
