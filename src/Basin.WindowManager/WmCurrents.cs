namespace Basin.WindowManager;

public sealed class WmCurrents
{
    private readonly RiverWindowManager _wm;
    private bool _layerDefaultSet;

    public WmCurrents(RiverWindowManager wm)
    {
        ArgumentNullException.ThrowIfNull(wm);
        _wm = wm;
    }

    public WmSeat? Seat { get; set; }

    public WmOutput? Output { get; set; }

    public bool SetsLayerDefault { get; init; }

    public void Follow(WmOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (SetsLayerDefault && _wm.LayerShell is not null
            && (!_layerDefaultSet || !ReferenceEquals(Output, output)))
        {
            output.SetDefaultForLayerSurfaces();
            _layerDefaultSet = true;
        }

        Output = output;
    }

    public void Update(ManageContext context, IReadOnlyList<WmOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(outputs);
        if (Seat is null or { IsRemoved: true })
        {
            Seat = context.Seats.Count > 0 ? context.Seats[0] : null;
        }

        if (Output is null or { IsRemoved: true })
        {
            Output = outputs.Count > 0 ? outputs[0] : null;
            _layerDefaultSet = false;
        }

        if (SetsLayerDefault && !_layerDefaultSet && _wm.LayerShell is not null
            && Output is { IsRemoved: false } defaultOutput)
        {
            defaultOutput.SetDefaultForLayerSurfaces();
            _layerDefaultSet = true;
        }

        if (_wm.LayerShell is { } shell)
        {
            foreach (var output in outputs)
            {
                shell.Track(output);
            }

            foreach (var seat in context.Seats)
            {
                shell.Track(seat);
            }
        }
    }
}
