using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoxLab.Core;

namespace VoxLab.App.Audio;

internal sealed record VoiceCheck(float[] Original,float[] Processed,int SampleRate)
{
    public const int Seconds=5;

    // This shared capture has no live output, WAV writer or loopback endpoint.
    public static async Task<VoiceCheck> CaptureAsync(RouteSettings route,VoiceSettings settings,CancellationToken token)
    {
        using var devices=new MMDeviceEnumerator();
        using var input=devices.GetDevice(route.InputId ?? throw new InvalidOperationException("Выберите микрофон в разделе «Устройства»."));
        if(input.DataFlow!=DataFlow.Capture) throw new InvalidOperationException("Выберите устройство ввода.");
        using var capture=new WasapiCapture(input,true,40);
        var format=capture.WaveFormat;
        if(format.Encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat) ||
            format.BitsPerSample is not (16 or 24 or 32) || format.SampleRate is <8000 or >192000)
            throw new InvalidOperationException("Формат микрофона не поддерживается для пробы.");
        var original=new float[Seconds*format.SampleRate];
        int position=0;
        var captured=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        capture.DataAvailable+=(_,e)=> {
            int count=Math.Min(original.Length-position,e.BytesRecorded/format.BlockAlign);
            AudioEngine.DecodeMono(e.Buffer,0,format,route.InputChannel,original.AsSpan(position,count));
            position+=count;
            if(position==original.Length) captured.TrySetResult();
        };
        capture.RecordingStopped+=(_,e)=>captured.TrySetException(e.Exception ?? new InvalidOperationException("Микрофон отключился во время пробы."));
        token.ThrowIfCancellationRequested();
        capture.StartRecording();
        try { await captured.Task.WaitAsync(TimeSpan.FromSeconds(10),token); }
        finally { capture.StopRecording(); }
        return Render(original,format.SampleRate,settings,token);
    }

    internal static VoiceCheck Render(float[] original,int rate,VoiceSettings settings,CancellationToken token)
    {
        var processor=new VoiceProcessor(rate); processor.Apply(settings);
        var processed=new float[original.Length]; var block=new float[32768]; int written=0;
        void Append(int count) {
            int take=Math.Min(count,processed.Length-written);
            block.AsSpan(0,take).CopyTo(processed.AsSpan(written)); written+=take;
        }
        for(int i=0;i<original.Length;i+=1024) {
            token.ThrowIfCancellationRequested();
            Append(processor.Process(original.AsSpan(i,Math.Min(1024,original.Length-i)),block));
        }
        Append(processor.Flush(block));
        return new(original,processed,rate);
    }

    public async Task PlayAsync(bool processed,string? monitorId,double volume,CancellationToken token)
    {
        var outputs=AudioEngine.Devices(DataFlow.Render);
        var target=outputs.FirstOrDefault(d=>d.Id==monitorId) ??
            outputs.FirstOrDefault(d=>d.IsDefault && !d.IsVirtual);
        if(target==null || target.IsVirtual)
            throw new InvalidOperationException("Выберите физические наушники для прослушивания в разделе «Устройства».");
        using var devices=new MMDeviceEnumerator();
        using var device=devices.GetDevice(target.Id);
        using var player=new WasapiOut(device,AudioClientShareMode.Shared,true,40);
        var samples=processed ? Processed : Original;
        // Apply only the monitor's listening level. Leave A/B level differences intact.
        var source=new SampleReader(samples,SampleRate,(float)Math.Clamp(volume/100,0,1));
        player.Init(source.ToWaveProvider());
        var ended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.PlaybackStopped+=(_,e)=> { if(e.Exception==null) ended.TrySetResult(); else ended.TrySetException(e.Exception); };
        token.ThrowIfCancellationRequested();
        player.Play();
        try { await ended.Task.WaitAsync(TimeSpan.FromSeconds(Seconds+5),token); }
        finally { player.Stop(); }
    }

    private sealed class SampleReader(float[] samples,int rate,float volume) : ISampleProvider
    {
        private int position;
        public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(rate,1);
        public int Read(float[] buffer,int offset,int count) {
            int take=Math.Min(count,samples.Length-position);
            for(int i=0;i<take;i++) buffer[offset+i]=Math.Clamp(samples[position+i],-1,1)*volume;
            position+=take;
            return take;
        }
    }
}
