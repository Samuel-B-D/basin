using Xunit;

namespace LunaWm.Tests;

public sealed class DeckTests
{
    internal sealed class FakeCard(string name) : ILayoutCard
    {
        public string Name { get; } = name;

        public CardPose Target { get; set; }

        public override string ToString() => Name;
    }

    private static CardMetrics Metrics() => new(1024, 768, 28, 0.659, 0.61, 30, 10, 0.8);

    private static (CardDeck<FakeCard> Deck, FakeCard[] Cards) Stack(int count)
    {
        var deck = new CardDeck<FakeCard>();
        var cards = new FakeCard[count];
        cards[0] = new FakeCard("c0");
        deck.Add(cards[0], null);
        for (var i = 1; i < count; i++)
        {
            cards[i] = new FakeCard($"c{i}");
            deck.Add(cards[i], cards[i - 1]);
        }

        return (deck, cards);
    }

    [Fact]
    public void The_card_scales_are_lunace_ratios_less_the_search_pill()
    {
        var metrics = Metrics();
        Assert.Equal(0.616254, metrics.ActiveScale, 6);
        Assert.Equal(0.570432, metrics.InactiveScale, 6);
        Assert.Equal(352.8, metrics.Origin, 6);
        Assert.Equal(new CardPose(512, 398, 1, 0, 1), metrics.Maximized);
    }

    [Fact]
    public void The_scale_never_drops_below_the_minimum()
    {
        var metrics = new CardMetrics(400, 80, 28, 0.659, 0.61, 30, 10, 0.8);
        Assert.Equal(CardMetrics.MinimumScale, metrics.ActiveScale);
    }

    [Theory]
    [InlineData(3, 1, new[] { -91.37, 0, 210.348 }, new[] { 0, 0, 14.023 }, new[] { -4.942, 0, 11.378 })]
    [InlineData(6, 1, new[] { -91.37, 0, 210.348, 420.696, 631.044, 697.764 }, new[] { 0, 0, 14.023, 28.046, 42.07, 46.518 }, new[] { -4.942, 0, 11.378, 22.756, 34.133, 37.742 })]
    [InlineData(6, 3, new[] { -175.509, -133.44, -91.37, 0, 210.348, 420.696 }, new[] { 0, 0, 0, 0, 14.023, 28.046 }, new[] { -9.493, -7.218, -4.942, 0, 11.378, 22.756 })]
    public void An_open_stack_fans_by_card_group_positions(int count, double position, double[] xs, double[] ys, double[] rotations)
    {
        var (deck, cards) = Stack(count);
        var stack = deck.ActiveStack!;
        stack.LayoutOpen(Metrics(), position, 0.8);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(xs[i], cards[i].Target.X, 2);
            Assert.Equal(ys[i], cards[i].Target.Y, 2);
            Assert.Equal(rotations[i], cards[i].Target.Rotation, 2);
            Assert.Equal(0.616254, cards[i].Target.Scale, 5);
        }
    }

    [Fact]
    public void A_closed_stack_peeks_the_last_three_cards_seven_pixels_apart()
    {
        var (deck, cards) = Stack(5);
        deck.ActiveStack!.LayoutClosed(Metrics(), 0.8);
        double[] expected = [-21, -21, -14, -7, 0];
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(expected[i], cards[i].Target.X);
            Assert.Equal(0.570432, cards[i].Target.Scale, 5);
            Assert.Equal(0.8, cards[i].Target.Brightness);
        }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0.5)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    public void The_stack_position_clamps_like_card_group(int count, double expected)
    {
        var (deck, _) = Stack(count);
        Assert.Equal(expected, deck.ActiveStack!.ClampPosition(9));
    }

    [Fact]
    public void A_long_stack_clamps_between_one_and_count_less_three()
    {
        var (deck, _) = Stack(7);
        Assert.Equal(1, deck.ActiveStack!.ClampPosition(-3));
        Assert.Equal(4, deck.ActiveStack.ClampPosition(9));
    }

    [Fact]
    public void A_sibling_joins_its_stack_in_front_and_becomes_active()
    {
        var deck = new CardDeck<FakeCard>();
        var a = new FakeCard("a");
        var b = new FakeCard("b");
        deck.Add(a, null);
        deck.Add(b, a);
        Assert.Single(deck.Stacks);
        Assert.Equal([a, b], deck.ActiveStack!.Cards);
        Assert.Same(b, deck.ActiveCard);
    }

    [Fact]
    public void Anything_else_starts_a_stack_right_of_the_active_one()
    {
        var deck = new CardDeck<FakeCard>();
        var a = new FakeCard("a");
        var b = new FakeCard("b");
        var c = new FakeCard("c");
        deck.Add(a, null);
        deck.Add(b, null);
        deck.Activate(a);
        deck.Add(c, null);
        Assert.Equal(["a", "c", "b"], deck.Stacks.Select(stack => stack.Cards[0].Name));
        Assert.Same(c, deck.ActiveCard);
    }

    [Theory]
    [InlineData(100, "app", false, 100, "other", true, false, "Auto", true)]
    [InlineData(101, "app", false, 100, "app", true, false, "Auto", true)]
    [InlineData(101, "other", false, 100, "app", true, false, "Auto", false)]
    [InlineData(100, "app", true, 100, "app", true, false, "Auto", false)]
    [InlineData(100, "app", false, 100, "app", false, false, "Auto", false)]
    [InlineData(100, "app", false, 100, "app", true, true, "Auto", false)]
    [InlineData(100, "app", false, 100, "app", true, false, "New", false)]
    [InlineData(101, "other", true, 100, "app", false, true, "Join", true)]
    public void The_grouping_rule(int pid, string appId, bool shellLaunched, int activePid, string activeAppId, bool focused, bool cardView, string choice, bool joins)
    {
        var candidate = new CardFacts(pid, appId, shellLaunched);
        var active = new CardFacts(activePid, activeAppId, false);
        Assert.Equal(joins, CardGrouping.JoinsActive(candidate, active, focused, cardView, Enum.Parse<StackChoice>(choice)));
    }

    [Fact]
    public void With_no_card_nothing_joins()
    {
        Assert.False(CardGrouping.JoinsActive(new CardFacts(1, "a", false), null, true, false, StackChoice.Join));
    }

    [Theory]
    [InlineData(-60, -1200, 300, "Close")]
    [InlineData(-40, -1200, 300, "SpringBack")]
    [InlineData(-200, -1000, 300, "SpringBack")]
    [InlineData(-400, -600, -50, "Close")]
    [InlineData(-400, -400, -50, "SpringBack")]
    [InlineData(80, 700, 400, "Maximize")]
    [InlineData(10, 0, 352, "SpringBack")]
    public void Flick_thresholds_follow_card_window_manager(double travel, double velocity, double centerY, string outcome)
    {
        Assert.Equal(Enum.Parse<FlickOutcome>(outcome), CardFlick.Decide(travel, velocity, centerY, 200, 768));
    }

    [Fact]
    public void Reorder_onto_another_stack_moves_the_card_and_empties_its_old_stack()
    {
        var deck = new CardDeck<FakeCard>();
        var a = new FakeCard("a");
        var b = new FakeCard("b");
        deck.Add(a, null);
        deck.Add(b, null);
        deck.MoveToStack(b, 0, 1);
        Assert.Single(deck.Stacks);
        Assert.Equal([a, b], deck.Stacks[0].Cards);
    }

    [Fact]
    public void Reorder_between_stacks_makes_a_new_one()
    {
        var (deck, cards) = Stack(3);
        deck.MoveToNewStack(cards[1], 1);
        Assert.Equal(2, deck.Stacks.Count);
        Assert.Equal([cards[0], cards[2]], deck.Stacks[0].Cards);
        Assert.Equal([cards[1]], deck.Stacks[1].Cards);
        Assert.Same(cards[1], deck.ActiveCard);
    }

    [Fact]
    public void Removing_the_active_card_activates_its_neighbor()
    {
        var (deck, cards) = Stack(3);
        deck.Remove(cards[2]);
        Assert.Same(cards[1], deck.ActiveCard);
        deck.Remove(cards[1]);
        deck.Remove(cards[0]);
        Assert.True(deck.IsEmpty);
        Assert.Null(deck.ActiveStack);
    }

    [Fact]
    public void Neighbors_run_through_stacks_in_order()
    {
        var deck = new CardDeck<FakeCard>();
        var a = new FakeCard("a");
        var b = new FakeCard("b");
        var c = new FakeCard("c");
        deck.Add(a, null);
        deck.Add(b, a);
        deck.Add(c, null);
        Assert.Same(b, deck.Neighbor(a, 1));
        Assert.Same(c, deck.Neighbor(b, 1));
        Assert.Same(b, deck.Neighbor(c, -1));
        Assert.Null(deck.Neighbor(c, 1));
    }

    [Fact]
    public void The_card_view_row_centers_the_active_stack_and_spaces_the_rest_by_the_gap()
    {
        var deck = new CardDeck<FakeCard>();
        var a = new FakeCard("a");
        var b = new FakeCard("b");
        deck.Add(a, null);
        deck.Add(b, null);
        var metrics = Metrics();
        deck.LayoutCardView(metrics, 0);
        Assert.Equal(512, b.Target.X, 3);
        Assert.Equal(352.8, b.Target.Y, 3);
        var halfActive = metrics.ActiveCardWidth / 2;
        var halfInactive = 1024 * metrics.InactiveScale / 2;
        Assert.Equal(512 - halfActive - 10 - halfInactive, a.Target.X, 3);
        Assert.Equal(0.8, a.Target.Brightness);
        Assert.Equal(1, b.Target.Brightness);
    }

    [Fact]
    public void A_layout_frame_allocates_nothing()
    {
        var (deck, cards) = Stack(6);
        var other = new FakeCard("x");
        deck.Add(other, null);
        deck.Activate(cards[2]);
        var metrics = Metrics();
        deck.LayoutCardView(metrics, 0);
        deck.LayoutMaximized(metrics, false);
        var motion = default(Motion);
        motion.Start(LunaMotion.CardSlide, 0, 1, 0, 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 1000; frame++)
        {
            deck.ActiveStack!.Position = 1 + (frame % 30 / 10.0);
            deck.LayoutCardView(metrics, frame % 200);
            motion.Advance(frame);
            foreach (var card in cards)
            {
                card.Target = CardPose.Lerp(card.Target, metrics.Maximized, motion.Value);
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
