using Basin.Notifications;
using Xunit;

namespace Basin.Tests;

public sealed class NotificationMarkupTests
{
    [Fact]
    public void Plain_text_passes_through_unchanged()
    {
        const string text = "no markup here: 3 > 2";
        Assert.Same(text, NotificationMarkup.Reduce(text, out var spans));
        Assert.Empty(spans);
    }

    [Fact]
    public void Nested_tags_become_spans_and_unknown_tags_are_dropped()
    {
        var text = NotificationMarkup.Reduce("<b>bold <i>both</i></b> <span color='red'>red</span> <u>open", out var spans);
        Assert.Equal("bold both red open", text);
        Assert.Equal(
            [
                new NotificationSpan(0, 9, NotificationSpanKind.Bold),
                new NotificationSpan(5, 4, NotificationSpanKind.Italic),
                new NotificationSpan(14, 4, NotificationSpanKind.Underline),
            ],
            spans);
    }

    [Fact]
    public void Entities_images_and_breaks_are_reduced()
    {
        var text = NotificationMarkup.Reduce("&#65;&#x42;&lt;&gt;&quot;&apos;&bogus; a<br/>b <img src=\"file:///x.png\" alt=\"[x]\"/> & c", out var spans);
        Assert.Equal("AB<>\"'&bogus; a\nb [x] & c", text);
        Assert.Equal([new NotificationSpan(18, 3, NotificationSpanKind.Image, "file:///x.png")], spans);
    }
}
