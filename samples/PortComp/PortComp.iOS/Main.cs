using Basin.Video.VideoToolbox;
using UIKit;

namespace PortComp.iOS;

public static class Program
{
    public static void Main(string[] args)
    {
        PortCompApp.VideoDecoder = VideoToolboxVideoDecoder.TryCreate(out var whyNot);
        PortCompApp.CarriesDmabuf = PortCompApp.VideoDecoder is not null;
        if (whyNot is not null)
        {
            Console.Error.WriteLine($"port-comp: no video: {whyNot}");
        }

        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
