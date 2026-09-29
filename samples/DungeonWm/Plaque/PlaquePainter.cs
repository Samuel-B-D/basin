using Prowl.Quill;
using Prowl.Scribe;
using Prowl.Vector;

namespace DungeonWm;

internal sealed class PlaquePainter
{
    private static readonly Color32 BrassLight = new(236, 198, 120, 255);
    private static readonly Color32 BrassDark = new(150, 108, 48, 255);
    private static readonly Color32 PolishLight = new(250, 222, 150, 255);
    private static readonly Color32 PolishDark = new(172, 126, 58, 255);
    private static readonly Color32 Rim = new(96, 66, 26, 255);
    private static readonly Color32 Engraving = new(58, 38, 12, 255);
    private static readonly Color32 Highlight = new(255, 236, 190, 160);
    private static readonly Color32 Boss = new(186, 142, 70, 255);
    private static readonly Color32 BossHot = new(252, 224, 150, 255);
    private static readonly Color32 BossPressed = new(128, 92, 38, 255);
    private static readonly string Ellipsis = "…";

    private readonly FontFile _font;

    public PlaquePainter(FontFile font)
    {
        _font = font;
    }

    public void Paint(Canvas canvas, in PlaqueState state)
    {
        var w = (float)state.Width;
        var h = (float)state.Height;
        var radius = h * 0.12f;
        canvas.SetLinearBrush(0, 0, 0, h, state.Focused ? PolishLight : BrassLight, state.Focused ? PolishDark : BrassDark);
        canvas.BeginPath();
        canvas.RoundedRect(0, 0, w, h, radius);
        canvas.Fill();
        canvas.ClearBrush();

        var inset = h * 0.06f;
        canvas.SetStrokeColor(Rim);
        canvas.SetStrokeWidth(MathF.Max(1.5f, h * 0.025f));
        canvas.BeginPath();
        canvas.RoundedRect(inset, inset, w - (2 * inset), h - (2 * inset), radius * 0.7f);
        canvas.Stroke();

        PaintTitle(canvas, state, w, h);
        foreach (var button in PlaqueLayout.Buttons)
        {
            PaintButton(canvas, button, state);
        }
    }

    private void PaintTitle(Canvas canvas, in PlaqueState state, float w, float h)
    {
        var left = PlaqueLayout.Margin(state.Height) + (h * 0.1f);
        var room = PlaqueLayout.TitleRight(state.Width, state.Height) - left;
        if (room <= h * 0.5f || string.IsNullOrEmpty(state.Title))
        {
            return;
        }

        var size = h * 0.38f;
        var text = Fit(canvas, state.Title, size, room);
        var baseline = h * 0.5f;
        canvas.DrawText(text, left, baseline + 1.5f, Highlight, size, _font, 0f, new Float2(0f, 0.5f));
        canvas.DrawText(text, left, baseline, Engraving, size, _font, 0f, new Float2(0f, 0.5f));
    }

    private string Fit(Canvas canvas, string title, float size, float room)
    {
        if (canvas.MeasureText(title, size, _font, 0f).X <= room)
        {
            return title;
        }

        var low = 0;
        var high = title.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (canvas.MeasureText(string.Concat(title.AsSpan(0, middle), Ellipsis), size, _font, 0f).X <= room)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return string.Concat(title.AsSpan(0, low).TrimEnd(), Ellipsis);
    }

    private static void PaintButton(Canvas canvas, PlaqueButton button, in PlaqueState state)
    {
        var (x, y, side) = PlaqueLayout.Rect(button, state.Width, state.Height);
        var pressed = state.Pressed == button;
        var hot = !pressed && state.Hover == button;
        var cx = x + (side * 0.5f);
        var cy = y + (side * 0.5f);
        var radius = side * 0.5f;
        canvas.CircleFilled(cx, cy, radius, Rim, 40);
        canvas.CircleFilled(cx, cy, radius * 0.9f, pressed ? BossPressed : hot ? BossHot : Boss, 40);
        canvas.SetStrokeColor(Engraving);
        canvas.SetStrokeWidth(MathF.Max(1.5f, side * 0.09f));
        var arm = radius * 0.42f;
        canvas.BeginPath();
        switch (button)
        {
            case PlaqueButton.Close:
                canvas.MoveTo(cx - arm, cy - arm);
                canvas.LineTo(cx + arm, cy + arm);
                canvas.MoveTo(cx - arm, cy + arm);
                canvas.LineTo(cx + arm, cy - arm);
                break;
            case PlaqueButton.Takeover:
                canvas.MoveTo(cx - arm, cy - arm);
                canvas.LineTo(cx + arm, cy - arm);
                canvas.LineTo(cx + arm, cy + arm);
                canvas.LineTo(cx - arm, cy + arm);
                canvas.ClosePath();
                break;
            case PlaqueButton.Throw:
                canvas.MoveTo(cx - arm, cy + arm);
                canvas.LineTo(cx + arm, cy - arm);
                canvas.MoveTo(cx + arm - (arm * 0.9f), cy - arm);
                canvas.LineTo(cx + arm, cy - arm);
                canvas.LineTo(cx + arm, cy - arm + (arm * 0.9f));
                break;
            case PlaqueButton.Rehang:
                const int Steps = 12;
                for (var i = 0; i <= Steps; i++)
                {
                    var angle = (-0.25f * MathF.PI) + (1.5f * MathF.PI * i / Steps);
                    var px = cx + (MathF.Cos(angle) * arm);
                    var py = cy + (MathF.Sin(angle) * arm);
                    if (i == 0)
                    {
                        canvas.MoveTo(px, py);
                    }
                    else
                    {
                        canvas.LineTo(px, py);
                    }
                }

                var tipAngle = 1.25f * MathF.PI;
                var tipX = cx + (MathF.Cos(tipAngle) * arm);
                var tipY = cy + (MathF.Sin(tipAngle) * arm);
                canvas.MoveTo(tipX, tipY);
                canvas.LineTo(tipX + (arm * 0.55f), tipY);
                canvas.MoveTo(tipX, tipY);
                canvas.LineTo(tipX, tipY + (arm * 0.55f));
                break;
        }

        canvas.Stroke();
    }
}
