using System.Numerics;
using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class TraceDecoderTests
{
    public const double RequiredTopThree = 0.90;

    private static WordList English()
    {
        Assert.SkipUnless(File.Exists(WordList.FcitxEnglish), "fcitx5's en_dict.fscd is not installed");
        return WordList.FromFscd(File.ReadAllBytes(WordList.FcitxEnglish));
    }

    internal static IReadOnlyList<string> CommonWords(WordList list, int count)
    {
        var indices = Enumerable.Range(0, list.Count)
            .Where(i => list[i].Length >= 2 && list[i].All(static c => c is >= 'a' and <= 'z'))
            .OrderByDescending(list.Prior)
            .ThenBy(i => list[i], StringComparer.Ordinal)
            .Take(count);
        return [.. indices.Select(i => list[i])];
    }

    internal static IReadOnlyList<string> SampledWords(WordList list, int count, int seed)
    {
        var pool = Enumerable.Range(0, list.Count)
            .Where(i => list[i].Length is >= 3 and <= 9 && list.Prior(i) >= 0.8f && list[i].All(static c => c is >= 'a' and <= 'z'))
            .Select(i => list[i])
            .ToList();
        var random = new Random(seed);
        var chosen = new List<string>(count);
        while (chosen.Count < count && pool.Count > 0)
        {
            var index = random.Next(pool.Count);
            chosen.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return chosen;
    }

    internal static Vector2[] PathThrough(string word, SoftKeyboardEngine engine, Random random, double jitter)
    {
        var points = new List<Vector2>();
        Vector2? previous = null;
        var unit = engine.Rects[Keys.Find(engine, "q")].Width;
        foreach (var c in word)
        {
            var rect = engine.Rects[Keys.Find(engine, c.ToString())];
            var center = new Vector2(
                rect.CenterX + (float)((random.NextDouble() * 2 - 1) * jitter * unit),
                rect.CenterY + (float)((random.NextDouble() * 2 - 1) * jitter * unit));
            if (previous is { } from)
            {
                var steps = Math.Max(1, (int)(Vector2.Distance(from, center) / 5));
                for (var i = 1; i <= steps; i++)
                {
                    points.Add(Vector2.Lerp(from, center, i / (float)steps));
                }
            }
            else
            {
                points.Add(center);
            }

            previous = center;
        }

        return [.. points];
    }

    [Fact]
    public void Two_hundred_jittered_paths_find_their_word_in_the_top_three()
    {
        var list = English();
        var decoder = new TraceDecoder(list);
        var engine = Keys.Engine();
        var random = new Random(1977);
        var words = SampledWords(list, 200, 7);
        var hits = 0;
        var misses = new List<string>();
        foreach (var word in words)
        {
            var path = PathThrough(word, engine, random, 0.25);
            var results = decoder.Decode(path, engine.Grid, engine.Rects);
            if (results.Take(3).Any(r => r.Word == word))
            {
                hits++;
            }
            else
            {
                misses.Add($"{word}->{string.Join('/', results.Take(3).Select(static r => r.Word))}");
            }
        }

        var rate = hits / (double)words.Count;
        TestContext.Current.SendDiagnosticMessage($"trace top-3: {hits}/{words.Count} = {rate:P1}; misses: {string.Join(' ', misses)}");
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "basin-trace-acceptance.txt"), $"{hits}/{words.Count} {string.Join(' ', misses)}");
        Assert.True(rate >= RequiredTopThree, $"top-3 {rate:P1} is below {RequiredTopThree:P0}: {string.Join(' ', misses)}");
    }

    [Fact]
    public void Exact_paths_put_their_word_first_for_most_common_words()
    {
        var list = English();
        var decoder = new TraceDecoder(list);
        var engine = Keys.Engine();
        var words = SampledWords(list, 600, 7);
        var first = words.Count(word => decoder.Decode(PathThrough(word, engine, new Random(0), 0), engine.Grid, engine.Rects) is { Count: > 0 } r && r[0].Word == word);
        TestContext.Current.SendDiagnosticMessage($"trace exact top-1: {first}/{words.Count}");
        Assert.True(first >= 540, $"exact top-1 {first}/{words.Count}");
    }

    [Fact]
    public void An_exact_path_through_hello_decodes_hello_first()
    {
        var decoder = new TraceDecoder(English());
        var engine = Keys.Engine();
        var path = PathThrough("hello", engine, new Random(1), 0);
        var results = decoder.Decode(path, engine.Grid, engine.Rects);
        Assert.True(results[0].Word == "hello", string.Join(' ', results.Select(static r => $"{r.Word}:{r.Score:0.00}")));
    }

    [Fact]
    public void A_flat_list_lets_the_shorter_word_win_a_tie()
    {
        var list = WordList.FromHunspell("SET UTF-8\n", "2\nto\ntoo\n");
        var decoder = new TraceDecoder(list);
        var engine = Keys.Engine();
        var results = decoder.Decode(PathThrough("to", engine, new Random(2), 0), engine.Grid, engine.Rects);
        Assert.Equal("to", results[0].Word);
    }
}
