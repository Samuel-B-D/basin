namespace Basin.Themes.MsStyles;

public readonly struct PeResourceKey : IEquatable<PeResourceKey>
{
    public PeResourceKey(int id)
    {
        Id = id;
        Name = null;
    }

    public PeResourceKey(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Id = -1;
        Name = name;
    }

    public int Id { get; }

    public string? Name { get; }

    public bool IsName => Name is not null;

    public static implicit operator PeResourceKey(int id) => new(id);

    public static implicit operator PeResourceKey(string name) => new(name);

    public bool Equals(PeResourceKey other) =>
        Name is null
            ? other.Name is null && Id == other.Id
            : other.Name is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is PeResourceKey other && Equals(other);

    public override int GetHashCode() =>
        Name is null ? Id : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public static bool operator ==(PeResourceKey left, PeResourceKey right) => left.Equals(right);

    public static bool operator !=(PeResourceKey left, PeResourceKey right) => !left.Equals(right);

    public override string ToString() => Name ?? Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
