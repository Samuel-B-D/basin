using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class WordListTests
{
    [Fact]
    public void The_installed_fscd_list_reads_with_its_weights()
    {
        Assert.SkipUnless(File.Exists(WordList.FcitxEnglish), "fcitx5's en_dict.fscd is not installed");
        var list = WordList.FromFscd(File.ReadAllBytes(WordList.FcitxEnglish));
        Assert.Equal(122_905, list.Count);
        Assert.Equal("a", list[0]);
        Assert.True(list.Weighted);
        var the = list.Words.ToList().IndexOf("the");
        var zebra = list.Words.ToList().IndexOf("zebra");
        Assert.True(list.Prior(the) > list.Prior(zebra));
    }

    [Fact]
    public void A_list_without_the_magic_is_refused()
    {
        Assert.Throws<FormatException>(() => WordList.FromFscd("NOTFSCD0\0\0\0\0"u8));
    }

    [Fact]
    public void Hunspell_stems_expand_through_one_level_of_affixes()
    {
        const string aff = """
            SET UTF-8
            NOSUGGEST !
            ONLYINCOMPOUND c
            NEEDAFFIX h

            PFX A Y 1
            PFX A   0     re         .

            SFX S Y 2
            SFX S   y     ies        [^aeiou]y
            SFX S   0     s          [^y]

            SFX D N 1
            SFX D   0     ed         .
            """;
        const string dic = """
            6
            try/SA
            play/SD
            fly/S
            rude!/S
            th/c
            bound/hS
            """;
        var list = WordList.FromHunspell(aff, dic);
        var words = list.Words.ToHashSet();
        Assert.Contains("try", words);
        Assert.Contains("tries", words);
        Assert.Contains("retry", words);
        Assert.Contains("retries", words);
        Assert.Contains("played", words);
        Assert.DoesNotContain("plaies", words);
        Assert.Contains("flies", words);
        Assert.DoesNotContain("rude", words);
        Assert.DoesNotContain("th", words);
        Assert.DoesNotContain("bound", words);
        Assert.Contains("bounds", words);
        Assert.False(list.Weighted);
    }

    [Fact]
    public void Long_flags_are_read_in_pairs()
    {
        const string aff = """
            FLAG long
            SFX Aa Y 1
            SFX Aa 0 s .
            FORBIDDENWORD {}
            """;
        const string dic = """
            2
            chat/Aa
            mot/{}
            """;
        var words = WordList.FromHunspell(aff, dic).Words.ToHashSet();
        Assert.Contains("chats", words);
        Assert.DoesNotContain("mot", words);
    }

    [Fact]
    public void A_language_with_no_list_says_why()
    {
        Assert.Null(WordList.ForLanguage("xx", out var reason));
        Assert.Contains("xx", reason, StringComparison.Ordinal);
    }
}
