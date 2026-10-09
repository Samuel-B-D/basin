using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace PortComp.Android;

[Application]
public sealed class PortCompApplication : AvaloniaAndroidApplication<PortCompApp>
{
    public PortCompApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        PortCompApp.VideoDecoder = Basin.Video.MediaCodec.MediaCodecVideoDecoder.TryCreate(out var whyNot);
        PortCompApp.CarriesDmabuf = PortCompApp.VideoDecoder is not null;
        if (whyNot is not null)
        {
            Console.Error.WriteLine($"port-comp: no video: {whyNot}");
        }

        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .With(new AndroidPlatformOptions
            {
                RenderingMode = [AndroidRenderingMode.Egl, AndroidRenderingMode.Software],
            });
}
