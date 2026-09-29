namespace Basin.Capabilities;

public readonly struct InputSettingValue : IEquatable<InputSettingValue>
{
    public const int MaxNumbers = 6;

    private readonly InputSettingNumbers _numbers;

    public InputSettingValue(uint value)
    {
        Value = value;
    }

    private InputSettingValue(uint value, ReadOnlySpan<double> numbers, string? text)
    {
        Value = value;
        Count = Math.Min(numbers.Length, MaxNumbers);
        for (var i = 0; i < Count; i++)
        {
            _numbers[i] = numbers[i];
        }

        TextValue = text;
    }

    public uint Value { get; }

    public int Count { get; }

    public string? TextValue { get; }

    public double this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return _numbers[index];
        }
    }

    public bool IsOn => Value != 0;

    public static InputSettingValue Flag(bool on) => new(on ? 1u : 0u);

    public static InputSettingValue Of(uint value) => new(value);

    public static InputSettingValue Of(InputClickMethod method) => new((uint)method);

    public static InputSettingValue Of(InputScrollMethod method) => new((uint)method);

    public static InputSettingValue Of(InputAccelProfile profile) => new((uint)profile);

    public static InputSettingValue Of(InputTapButtonMap map) => new((uint)map);

    public static InputSettingValue Of(InputClickfingerButtonMap map) => new((uint)map);

    public static InputSettingValue Of(InputDragLock state) => new((uint)state);

    public static InputSettingValue Of(InputThreeFingerDrag state) => new((uint)state);

    public static InputSettingValue Of(InputSendEventsMode mode) => new((uint)mode);

    public static InputSettingValue Number(double value) => new(0, [value], null);

    public static InputSettingValue Matrix(ReadOnlySpan<double> six)
    {
        if (six.Length != MaxNumbers)
        {
            throw new ArgumentException("a calibration matrix has six numbers", nameof(six));
        }

        return new InputSettingValue(0, six, null);
    }

    public static InputSettingValue Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new InputSettingValue(0, default, text);
    }

    public void CopyNumbers(Span<double> into)
    {
        for (var i = 0; i < Count && i < into.Length; i++)
        {
            into[i] = _numbers[i];
        }
    }

    public bool Equals(InputSettingValue other)
    {
        if (Value != other.Value || Count != other.Count || !string.Equals(TextValue, other.TextValue, StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < Count; i++)
        {
            if (_numbers[i] != other._numbers[i])
            {
                return false;
            }
        }

        return true;
    }

    public bool Matches(in InputSettingValue other, double tolerance = 1e-6)
    {
        if (Value != other.Value || Count != other.Count || !string.Equals(TextValue, other.TextValue, StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < Count; i++)
        {
            if (Math.Abs(_numbers[i] - other._numbers[i]) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is InputSettingValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Value);
        hash.Add(Count);
        hash.Add(TextValue, StringComparer.Ordinal);
        for (var i = 0; i < Count; i++)
        {
            hash.Add(_numbers[i]);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => TextValue ?? (Count > 0 ? _numbers[0].ToString(System.Globalization.CultureInfo.InvariantCulture) : Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static bool operator ==(InputSettingValue left, InputSettingValue right) => left.Equals(right);

    public static bool operator !=(InputSettingValue left, InputSettingValue right) => !left.Equals(right);
}
