using Basin.Freedesktop;
using Xunit;

namespace Basin.Tests;

public sealed class RecentlyUsedTests
{
    [Fact]
    public void File_bookmarks_come_back_newest_first_with_their_mime_type()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recently-used-{Guid.NewGuid():N}.xbel");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <xbel version="1.0" xmlns:bookmark="http://www.freedesktop.org/standards/desktop-bookmarks" xmlns:mime="http://www.freedesktop.org/standards/shared-mime-info">
              <bookmark href="file:///home/u/old%20notes.txt" modified="2026-01-01T10:00:00Z">
                <info><metadata owner="http://freedesktop.org"><mime:mime-type type="text/plain"/></metadata></info>
              </bookmark>
              <bookmark href="https://example.org/" modified="2026-06-01T10:00:00Z"/>
              <bookmark href="file:///home/u/Projects" modified="2026-03-01T10:00:00Z">
                <info><metadata owner="http://freedesktop.org"><mime:mime-type type="inode/directory"/></metadata></info>
              </bookmark>
            </xbel>
            """);
        try
        {
            var items = RecentlyUsed.Read(path);
            Assert.Equal(2, items.Count);
            Assert.Equal("/home/u/Projects", items[0].Path);
            Assert.True(items[0].IsDirectory);
            Assert.Equal("/home/u/old notes.txt", items[1].Path);
            Assert.Equal("text/plain", items[1].MimeType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_missing_file_is_no_items() =>
        Assert.Empty(RecentlyUsed.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.xbel")));
}
