using Basin;
using Basin.Scene;

namespace LunaWm;

internal sealed class CardShadow
{
    public const int Spread = 20;
    public const int OffsetY = 5;
    public const int Tile = 87;
    public const int Margin = Tile / 2;

    private static readonly byte[] Profile =
    [
        0, 0, 0, 1, 1, 2, 4, 6, 7, 8, 12, 19, 24, 28, 35, 43, 53, 60, 70, 79, 87, 96,
        103, 111, 118, 124, 129, 134, 138, 142, 144, 148, 150, 152, 153, 153, 155, 156,
        157, 157, 158, 159, 160, 160,
    ];

    private readonly SceneBuffer[] _slices = new SceneBuffer[9];

    public CardShadow(SceneTree parent, IBuffer tile)
    {
        Tree = new SceneTree(parent);
        for (var i = 0; i < _slices.Length; i++)
        {
            _slices[i] = new SceneBuffer(Tree) { InputEnabled = false };
            _slices[i].SetBuffer(tile);
        }
    }

    public SceneTree Tree { get; }

    public static MemoryBuffer CreateTile()
    {
        var buffer = new MemoryBuffer(Tile, Tile, DrmFormat.Argb8888);
        if (!buffer.BeginDataAccess(BufferDataAccess.Write, out var view))
        {
            return buffer;
        }

        try
        {
            unsafe
            {
                var bytes = (byte*)view.Data;
                for (var y = 0; y < Tile; y++)
                {
                    var row = (uint*)(bytes + (y * view.Stride));
                    var py = Profile[Math.Min(y, Tile - 1 - y)];
                    for (var x = 0; x < Tile; x++)
                    {
                        var px = Profile[Math.Min(x, Tile - 1 - x)];
                        row[x] = (uint)(px * py / 160) << 24;
                    }
                }
            }
        }
        finally
        {
            buffer.EndDataAccess();
        }

        return buffer;
    }

    public static MemoryBuffer CreateBlack()
    {
        var buffer = new MemoryBuffer(1, 1, DrmFormat.Argb8888);
        if (buffer.BeginDataAccess(BufferDataAccess.Write, out var view))
        {
            unsafe
            {
                *(uint*)view.Data = 0xff000000;
            }

            buffer.EndDataAccess();
        }

        return buffer;
    }

    public void Place(int width, int height)
    {
        var x0 = -Spread;
        var y0 = -Spread + OffsetY;
        var w = width + (2 * Spread);
        var h = height + (2 * Spread);
        var middleW = Math.Max(0, w - (2 * Margin));
        var middleH = Math.Max(0, h - (2 * Margin));
        int[] xs = [x0, x0 + Margin, x0 + Margin + middleW];
        int[] ys = [y0, y0 + Margin, y0 + Margin + middleH];
        int[] ws = [Margin, middleW, Margin];
        int[] hs = [Margin, middleH, Margin];
        double[] sx = [0, Margin, Tile - Margin];
        double[] sw = [Margin, 1, Margin];
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var slice = _slices[(row * 3) + column];
                slice.SourceBox = new FBox(sx[column], sx[row], sw[column], sw[row]);
                slice.DestinationWidth = Math.Max(1, ws[column]);
                slice.DestinationHeight = Math.Max(1, hs[row]);
                slice.Enabled = ws[column] > 0 && hs[row] > 0;
                slice.SetPosition(xs[column], ys[row]);
            }
        }
    }
}
