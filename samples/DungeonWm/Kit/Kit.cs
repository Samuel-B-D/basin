using System.Reflection;
using System.Security.Cryptography;
using Basin.Diagnostics;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed class Kit
{
    private readonly Dictionary<string, KitModel> _models;

    private Kit(Dictionary<string, KitModel> models, IReadOnlyList<WorldImage> images)
    {
        _models = models;
        Images = images;
    }

    public IReadOnlyList<WorldImage> Images { get; }

    public IEnumerable<KitModel> Models => _models.Values;

    public KitModel this[string name] => _models.TryGetValue(name, out var model)
        ? model
        : throw new KeyNotFoundException($"The kit has no piece named {name}.");

    public static Kit Load()
    {
        var images = new Dictionary<string, WorldImage>(StringComparer.Ordinal);
        var models = new Dictionary<string, KitModel>(StringComparer.Ordinal);
        foreach (var piece in KitManifest.Pieces)
        {
            var document = GlbDocument.Parse(ReadResource(piece.Resource));
            var image = (document.Root.Images?.Length ?? 0) > 0 ? SharedImage(document, 0, images) : null;
            models[piece.Name] = new KitModel(piece, GlbMeshes.Flatten(document, piece.Hidden, image));
        }

        return new Kit(models, [.. images.Values]);
    }

    public static byte[] ReadResource(string name)
    {
        using var stream = typeof(Kit).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded resource {name} is missing.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public static WorldImage DecodeImage(GlbDocument document, int image)
    {
        var (rgba, width, height) = PngCodec.Decode(document.ReadImage(image));
        return new WorldImage(width, height, rgba);
    }

    private static WorldImage SharedImage(GlbDocument document, int image, Dictionary<string, WorldImage> images)
    {
        var bytes = document.ReadImage(image);
        var key = Convert.ToHexString(SHA256.HashData(bytes));
        if (!images.TryGetValue(key, out var decoded))
        {
            decoded = DecodeImage(document, image);
            images[key] = decoded;
        }

        return decoded;
    }
}
