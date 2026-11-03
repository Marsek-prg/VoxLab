using NAudio.Wave;

namespace VoxLab.App.Audio;

/// <summary>Bounded queue between independently clocked audio endpoints. Silence on starvation.</summary>
public sealed class AudioRingBuffer : IWaveProvider
{
    private readonly object sync=new();
    private readonly float[] data;
    private readonly int prebuffer;
    private int read, write, count;
    private bool primed, hasReceived;
    public WaveFormat WaveFormat { get; }
    public long Underruns { get; private set; }
    public long Overruns { get; private set; }
    public double BufferedMilliseconds { get { lock(sync) return count*1000.0/WaveFormat.SampleRate; } }
    public AudioRingBuffer(int rate,int prebufferMs)
    {
        WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(rate,1);
        data=new float[rate/2]; prebuffer=rate*prebufferMs/1000;
    }
    public void Add(ReadOnlySpan<float> values)
    {
        lock(sync)
        {
            if(values.Length==0) return;
            hasReceived=true;
            if(values.Length>data.Length) { values=values[^data.Length..]; Overruns++; }
            // Drop oldest data rather than letting latency grow without bound.
            int excess=count+values.Length-data.Length;
            if(excess>0) { read=(read+excess)%data.Length; count-=excess; Overruns++; }
            int first=Math.Min(values.Length,data.Length-write);
            values[..first].CopyTo(data.AsSpan(write));
            values[first..].CopyTo(data);
            write=(write+values.Length)%data.Length;
            count+=values.Length;
        }
    }
    public int Read(byte[] buffer,int offset,int bytes)
    {
        lock(sync)
        {
            var destination=buffer.AsSpan(offset,bytes); destination.Clear();
            if(!primed && count>=prebuffer) primed=true;
            if(!primed) return bytes;
            int requested=bytes/4, available=Math.Min(count,requested);
            int first=Math.Min(available,data.Length-read);
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan(read,first)).CopyTo(destination);
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan(0,available-first)).CopyTo(destination[(first*4)..]);
            read=(read+available)%data.Length;
            count-=available;
            if(available<requested && hasReceived) { Underruns++; primed=false; }
            return bytes;
        }
    }
}
