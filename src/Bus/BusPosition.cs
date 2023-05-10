namespace Logship.WmataPuller
{ 
    internal record BusPositionsWrapper(IReadOnlyList<BusPosition> BusPositions) { }

    internal record BusPosition(
        DateTime DateTime,
        double Deviation,
        string DirectionText,
        double Lat,
        double Lon,
        string RouteID,
        DateTime TripEndTime,
        string TreipHeadsign,
        string TripID,
        DateTime TripStartTime,
        string VehicleID)
    {
    }
}
