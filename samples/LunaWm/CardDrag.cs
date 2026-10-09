namespace LunaWm;

internal sealed class CardDrag
{
    public required LunaView View { get; init; }

    public required int Id { get; init; }

    public Card? Card { get; init; }

    public required double StartX { get; set; }

    public required double StartY { get; set; }

    public required uint StartTime { get; init; }

    public DragAxis Axis { get; set; }

    public double StartPosition { get; set; }

    public double StartScroll { get; set; }

    public CardPose StartPose { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public Basin.Seat.VelocityTracker Tracker;

    public Basin.IEventSource? HoldTimer { get; set; }
}
