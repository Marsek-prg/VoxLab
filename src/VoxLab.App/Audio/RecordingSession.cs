using System.Buffers;
using System.Threading.Channels;
using NAudio.Wave;

namespace VoxLab.App.Audio;

/// <summary>Disk I/O never blocks the capture callback. A full queue fails explicitly.</summary>
public sealed class RecordingSession : IDisposable
{
    private readonly Channel<(byte[] Buffer,int Count)> queue=Channel.CreateBounded<(byte[],int)>(256);
    private readonly Task writerTask;
    private long bytesWritten;
    private readonly int rate;
    private volatile string? error;
    public string Path { get; }
    public string? Error=>error;
    public double Seconds=>Interlocked.Read(ref bytesWritten)/(rate*4.0);

    public RecordingSession(string path,int sampleRate)
    {
        Path=path; rate=sampleRate;
        var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(sampleRate,1));
        writerTask=Task.Run(async()=>
        {
            try
            {
                await foreach(var block in queue.Reader.ReadAllAsync())
                {
                    try { writer.Write(block.Buffer,0,block.Count); Interlocked.Add(ref bytesWritten,block.Count); }
                    finally { ArrayPool<byte>.Shared.Return(block.Buffer); }
                }
            }
            catch(Exception ex) { error="Не удалось сохранить запись: "+ex.Message; queue.Writer.TryComplete(ex); }
            finally
            {
                while(queue.Reader.TryRead(out var block)) ArrayPool<byte>.Shared.Return(block.Buffer);
                try { writer.Dispose(); } catch(Exception ex) { error="Ошибка завершения WAV: "+ex.Message; }
            }
        });
    }
    public void Add(ReadOnlySpan<float> samples)
    {
        if(samples.Length==0 || error!=null) return;
        // Classic WAV uses a 32-bit length. Stop cleanly well before 4 GiB.
        if(Interlocked.Read(ref bytesWritten)>3_900_000_000) { error="Достигнут предел WAV (3,9 ГБ). Начните новую запись."; queue.Writer.TryComplete(); return; }
        int length=samples.Length*4;
        byte[] buffer=ArrayPool<byte>.Shared.Rent(length);
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples).CopyTo(buffer);
        if(!queue.Writer.TryWrite((buffer,length)))
        {
            ArrayPool<byte>.Shared.Return(buffer);
            error="Диск не успевает сохранять звук. Запись остановлена без блокировки микрофона.";
            queue.Writer.TryComplete();
        }
    }
    public void Dispose() { queue.Writer.TryComplete(); writerTask.GetAwaiter().GetResult(); }
}
