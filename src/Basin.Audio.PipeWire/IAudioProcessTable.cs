namespace Basin.Audio.PipeWire;

public interface IAudioProcessTable
{
    int ParentOf(int pid);

    int GroupOf(int pid);

    string? FlatpakAppOf(int pid);
}
