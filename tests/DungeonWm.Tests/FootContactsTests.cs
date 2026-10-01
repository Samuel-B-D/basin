using Basin.Scene3D.Gltf;
using Xunit;

namespace DungeonWm.Tests;

public sealed class FootContactsTests
{
    private static readonly Lazy<GlbAvatarModel> Model = new(() =>
        GlbAvatar.Load(Kit.ReadResource(KitManifest.Avatar), 30, ["Rogue_Cape"], Kit.DecodeImage));

    [Theory]
    [InlineData("Walking_A", 1.9)]
    [InlineData("Running_A", 2.5)]
    public void Each_foot_lands_once_a_cycle_half_a_cycle_after_the_other(string clip, double stepsPerSecond)
    {
        var contacts = FootContacts.Find(Model.Value.Skeleton, Model.Value.Clips[clip]);

        Assert.NotNull(contacts);
        Assert.Equal(2, contacts.Times.Length);
        Assert.NotEqual(contacts.Feet[0], contacts.Feet[1]);
        Assert.InRange(contacts.Times[1] - contacts.Times[0], contacts.Duration * 0.4, contacts.Duration * 0.6);
        Assert.InRange(2 / contacts.Duration, stepsPerSecond - 0.2, stepsPerSecond + 0.2);
    }

    [Fact]
    public void A_contact_is_crossed_once_across_the_loop()
    {
        var contacts = FootContacts.Find(Model.Value.Skeleton, Model.Value.Clips["Walking_A"])!;
        var first = contacts.Times[0];

        Assert.Equal(contacts.Feet[0], contacts.Crossed(first - 0.01f, first + 0.01f));
        Assert.Equal(contacts.Feet[0], contacts.Crossed(contacts.Duration * 3 + first - 0.01f, contacts.Duration * 3 + first));
        Assert.Equal(-1, contacts.Crossed(first + 0.01f, first + 0.02f));
        Assert.Equal(-1, contacts.Crossed(first, first));

        var steps = 0;
        for (var t = 0f; t < contacts.Duration * 10; t += 1f / 144)
        {
            steps += contacts.Crossed(t, t + (1f / 144)) >= 0 ? 1 : 0;
        }

        Assert.Equal(20, steps);
    }
}
