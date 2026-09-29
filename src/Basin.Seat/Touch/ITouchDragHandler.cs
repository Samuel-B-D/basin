namespace Basin.Seat;

public interface ITouchDragHandler
{
    void DragTo(uint timeMs, double x, double y);

    void DragEnd(uint timeMs, bool cancelled);
}
