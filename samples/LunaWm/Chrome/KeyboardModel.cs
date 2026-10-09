using Basin.SoftKeyboard;

namespace LunaWm;

public sealed class KeyboardModel
{
    public const float PopupRoom = 160;

    public KeyboardModel(SoftKeyboardEngine engine)
    {
        Engine = engine;
    }

    public SoftKeyboardEngine Engine { get; }

    public KeyboardLayout? Layout { get; set; }

    public KeyboardSize Size { get; set; } = KeyboardSize.Medium;

    public float Width { get; set; }

    public float Height { get; set; }

    public float Padding { get; set; }

    public float BarHeight { get; set; }

    public float KeymapTop => PopupRoom + BarHeight + Padding;

    public float KeyboardTop => PopupRoom + BarHeight;

    public string EnterLabel { get; set; } = "Enter";

    public IReadOnlyList<string> Candidates { get; set; } = [];

    public int CandidateCursor { get; set; }

    public string? Preedit { get; set; }

    public float CandidateScroll { get; set; }

    public List<(double Left, double Right)> CandidateSpans { get; } = [];

    public double CandidateContentWidth { get; set; }

    public int CandidateAt(double x)
    {
        var content = x + CandidateScroll;
        for (var i = 0; i < CandidateSpans.Count; i++)
        {
            if (content >= CandidateSpans[i].Left - 9 && content < CandidateSpans[i].Right + 9)
            {
                return i;
            }
        }

        return -1;
    }

    public float TraceAlpha { get; set; }

    public event Action? Changed;

    public void Invalidate() => Changed?.Invoke();
}
