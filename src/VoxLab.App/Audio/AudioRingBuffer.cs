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
            for(int i=0;i<values.Length;i++) { data[write]=values[i]; write=(write+1)%data.Length; }
            count+=values.Length;
        }
    }
    public int Read(byte[] buffer,int offset,int bytes)
    {
        lock(sync)
        {
            Array.Clear(buffer,offset,bytes);
            if(!primed && count>=prebuffer) primed=true;
            if(!primed) return bytes;
            int requested=bytes/4, available=Math.Min(count,requested);
            for(int i=0;i<available;i++)
            {
                BitConverter.TryWriteBytes(buffer.AsSpan(offset+i*4,4),data[read]);
                read=(read+1)%data.Length;
            }
            count-=available;
            if(available<requested && hasReceived) { Underruns++; primed=false; }
            return bytes;
        }
    }
}
