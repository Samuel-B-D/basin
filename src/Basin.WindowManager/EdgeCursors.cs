using Basin.WindowManager.Protocol;

namespace Basin.WindowManager;

public static class EdgeCursors
{
    public static WpCursorShapeDeviceV1.Shape ShapeFor(Edges edges) => edges switch
    {
        Edges.Top | Edges.Left => WpCursorShapeDeviceV1.Shape.NwResize,
        Edges.Top | Edges.Right => WpCursorShapeDeviceV1.Shape.NeResize,
        Edges.Bottom | Edges.Left => WpCursorShapeDeviceV1.Shape.SwResize,
        Edges.Bottom | Edges.Right => WpCursorShapeDeviceV1.Shape.SeResize,
        Edges.Left => WpCursorShapeDeviceV1.Shape.WResize,
        Edges.Right => WpCursorShapeDeviceV1.Shape.EResize,
        Edges.Top => WpCursorShapeDeviceV1.Shape.NResize,
        Edges.Bottom => WpCursorShapeDeviceV1.Shape.SResize,
        Edges.Left | Edges.Right => WpCursorShapeDeviceV1.Shape.EwResize,
        Edges.Top | Edges.Bottom => WpCursorShapeDeviceV1.Shape.NsResize,
        _ => WpCursorShapeDeviceV1.Shape.Default,
    };
}
