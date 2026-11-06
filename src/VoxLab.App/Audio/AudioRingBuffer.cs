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
    private int reserve, warmFrames;
    private double meanFill, fraction;
    public WaveFormat WaveFormat { get; }
    public long Underruns { get; private set; }
    public long Overruns { get; private set; }
    public double BufferedMilliseconds { get { lock(sync) return count*1000.0/WaveFormat.SampleRate; } }
    public AudioRingBuffer(int rate,int prebufferMs)
    {
        WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(rate,1);
        data=new float[rate/2]; reserve=prebuffer=rate*prebufferMs/1000;
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
            if(excess>0) { read=(read+excess)%data.Length; count-=excess; fraction=0; Overruns++; }
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
            int requested=bytes/4,rate=WaveFormat.SampleRate;
            if(!primed && count>=Math.Max(reserve,Math.Min(requested,data.Length))) { primed=true; meanFill=count; warmFrames=0; }
            if(!primed) return bytes;
            // Slowly follow each playback endpoint's clock. A low-pass at one second
            // rejects normal WASAPI packet jitter; do not change recorded sample timing.
            double target=Math.Max(reserve,requested),step=1;
            meanFill+=(count-meanFill)*(1-Math.Exp(-requested/(double)rate));
            if(warmFrames>=rate/2 && requested<=rate/8) {
                double error=(meanFill-target)/rate;
                step+=Math.Clamp(Math.CopySign(Math.Max(0,Math.Abs(error)-.002),error)/3,-.003,.003);
            }
            warmFrames=Math.Min(rate,warmFrames+requested);
            int produced=0;
            if(step==1 && fraction==0) {
                produced=Math.Min(count,requested);
                int first=Math.Min(produced,data.Length-read);
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan(read,first)).CopyTo(destination);
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan(0,produced-first)).CopyTo(destination[(first*4)..]);
                read=(read+produced)%data.Length; count-=produced;
            } else {
                var output=System.Runtime.InteropServices.MemoryMarshal.Cast<byte,float>(destination[..(requested*4)]);
                while(produced<requested && count>0) {
                    int next=read+1==data.Length?0:read+1;
                    float a=data[read]; output[produced++]=count==1?a:(float)(a+fraction*(data[next]-a));
                    fraction+=step; int advance=(int)fraction; fraction-=advance;
                    advance=Math.Min(advance,count); read=(read+advance)%data.Length; count-=advance;
                }
            }
            if(produced<requested && hasReceived) {
                Underruns++; primed=false; fraction=0;
                reserve=Math.Min(Math.Max(prebuffer,rate*120/1000),reserve+Math.Min(requested,rate/50));
            }
            return bytes;
        }
    }
}
