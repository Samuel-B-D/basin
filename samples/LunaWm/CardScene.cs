using Basin.Scene;

namespace LunaWm;

internal sealed class CardScene
{
    public CardScene(SceneTree parent, Basin.IBuffer? shadowTile, Basin.IBuffer black)
    {
        Slot = new SceneTree(parent) { Enabled = false };
        Frame = new SceneTransform(Slot);
        Shadow = shadowTile is null ? null : new CardShadow(Frame, shadowTile);
        Content = new SceneTree(Frame);
        Dim = new SceneTree(Frame) { Enabled = false };
        DimFill = Fill(Dim, black);
        Transients = new SceneTree(Frame);
        ScrimTree = new SceneTree(Transients) { Enabled = false, Alpha = 0.5f };
        Scrim = Fill(ScrimTree, black);
    }

    public SceneTree Slot { get; }

    public SceneTransform Frame { get; }

    public CardShadow? Shadow { get; }

    public SceneTree Content { get; }

    public SceneTree Transients { get; }

    public SceneTree ScrimTree { get; }

    public SceneBuffer Scrim { get; }

    public SceneTree Dim { get; }

    public SceneBuffer DimFill { get; }

    public void Size(int width, int height)
    {
        foreach (var fill in new[] { Scrim, DimFill })
        {
            fill.DestinationWidth = width;
            fill.DestinationHeight = height;
        }
    }

    private static SceneBuffer Fill(SceneTree parent, Basin.IBuffer black)
    {
        var fill = new SceneBuffer(parent) { InputEnabled = false };
        fill.SetBuffer(black);
        fill.SourceBox = new Basin.FBox(0, 0, 1, 1);
        return fill;
    }
}
