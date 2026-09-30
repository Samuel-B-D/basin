namespace Basin.WindowManager;

public static class WmOutputPolicy
{
    public static Rect UsableArea(WmOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var area = output.NonExclusiveArea;
        return area.IsEmpty ? output.Area : area;
    }

    public static WmOutput? LargestOverlap(Rect rect, IReadOnlyList<WmOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        WmOutput? best = null;
        long bestArea = 0;
        foreach (var output in outputs)
        {
            var overlap = rect.Intersect(output.Area);
            var area = (long)overlap.Width * overlap.Height;
            if (!overlap.IsEmpty && area > bestArea)
            {
                bestArea = area;
                best = output;
            }
        }

        return best;
    }
}
