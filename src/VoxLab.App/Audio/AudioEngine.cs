using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoxLab.Core;

namespace VoxLab.App.Audio;

public sealed record AudioDevice(string Id,string Name,bool IsDefault,bool IsVirtual);
public sealed record RouteSettings(string? InputId=null,string? OutputId=null,string? MonitorId=null,
    bool MonitorEnabled=false,double MonitorVolume=25,int LatencyMs=40,int InputChannel=-1);

public sealed class AudioEngine : IDisposable
{
    private WasapiCapture? capture;
    private WasapiOut? output,monitor;
    private MMDevice? inputDevice,outputDevice,monitorDevice;
    private AudioRingBuffer? mainBuffer,monitorBuffer;
    private VoiceProcessor? processor;
    private VoiceSettings settings=new(),applied=new();
    private readonly object processingLock=new();
    private readonly object lifecycleLock=new();
    private bool disposed;
    private readonly float[] decoded=new float[8192],processed=new float[32768],monitorSamples=new float[32768];
    private readonly float[] scope=new float[128];
    private RecordingSession? recording;
    private volatile bool running,stopping;
    private volatile bool bypass,muted;
    private volatile string? lastError;
    private float monitorVolume=.25f;
    private RouteSettings route=new();
    private int sampleRate=48000;
    private WaveFormat? inputFormat;
    private double dspLoad;
    public bool IsRunning=>running;
    public bool Bypass { get=>bypass; set=>bypass=value; }
    public bool Muted { get=>muted; set=>muted=value; }
    public bool IsRecording { get { lock(processingLock) return recording!=null; } }
    public string? RecordingError { get { lock(processingLock) return recording?.Error; } }
    public string? LastRecordingPath { get; private set; }
    public string? Error=>lastError;
    public int SampleRate=>sampleRate;

    public static IReadOnlyList<AudioDevice> Devices(DataFlow flow)
    {
        using var enumerator=new MMDeviceEnumerator();
        string? defaultId=null;
        try { using var device=enumerator.GetDefaultAudioEndpoint(flow,Role.Multimedia); defaultId=device.ID; } catch { /* No default endpoint. */ }
        return enumerator.EnumerateAudioEndPoints(flow,DeviceState.Active).Select(d=>
        {
            using(d) return new AudioDevice(d.ID,d.FriendlyName,d.ID==defaultId,
                d.FriendlyName.Contains("CABLE",StringComparison.OrdinalIgnoreCase) ||
                d.FriendlyName.Contains("VoiceMeeter",StringComparison.OrdinalIgnoreCase) ||
                d.FriendlyName.Contains("Virtual",StringComparison.OrdinalIgnoreCase));
        }).ToArray();
    }

    public void Update(VoiceSettings value,bool studioProcessing=true)=>Volatile.Write(ref settings,(studioProcessing ? value : value.WithoutStudioProcessing()).Validate());
    public void SetMonitorVolume(double percent)=>monitorVolume=(float)Math.Clamp(percent/100,0,1);

    public void Start(RouteSettings value)
    {
        lock(lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            StartCore(value);
        }
    }
    private void StartCore(RouteSettings value)
    {
        Stop(); lastError=null; stopping=false;
        dspLoad=0; Array.Clear(scope);
        route=value with { LatencyMs=value.LatencyMs is 20 or 40 or 80 ? value.LatencyMs : 40 };
        if(string.IsNullOrWhiteSpace(route.InputId) || string.IsNullOrWhiteSpace(route.OutputId))
            throw new InvalidOperationException("Выберите микрофон и выход в разделе «Устройства».");
        if(route.MonitorEnabled && route.MonitorId==route.OutputId)
            throw new InvalidOperationException("Для прослушивания выберите другой выход, чтобы не дублировать звук.");
        try
        {
            using var enumerator=new MMDeviceEnumerator();
            inputDevice=enumerator.GetDevice(route.InputId); outputDevice=enumerator.GetDevice(route.OutputId);
            if(inputDevice.DataFlow!=DataFlow.Capture || outputDevice.DataFlow!=DataFlow.Render)
                throw new InvalidOperationException("Неверный тип аудиоустройства. Обновите список устройств.");
            capture=new WasapiCapture(inputDevice,true,route.LatencyMs);
            inputFormat=capture.WaveFormat;
            sampleRate=inputFormat.SampleRate;
            if(capture.WaveFormat.Encoding!=WaveFormatEncoding.IeeeFloat && capture.WaveFormat.Encoding!=WaveFormatEncoding.Pcm)
                throw new InvalidOperationException("Формат микрофона не поддерживается. В Windows выберите PCM 16/24/32 бит.");
            if(capture.WaveFormat.BitsPerSample is not (16 or 24 or 32))
                throw new InvalidOperationException("Выберите для микрофона 16, 24 или 32 бита в настройках Windows.");
            processor=new VoiceProcessor(sampleRate);
            applied=Volatile.Read(ref settings); processor.Apply(applied);
            mainBuffer=new AudioRingBuffer(sampleRate,route.LatencyMs);
            output=new WasapiOut(outputDevice,AudioClientShareMode.Shared,true,route.LatencyMs);
            output.Init(mainBuffer);
            output.PlaybackStopped+=OnPlaybackStopped;
            if(route.MonitorEnabled)
            {
                if(string.IsNullOrWhiteSpace(route.MonitorId)) throw new InvalidOperationException("Выберите наушники для прослушивания.");
                monitorDevice=enumerator.GetDevice(route.MonitorId);
                monitorBuffer=new AudioRingBuffer(sampleRate,route.LatencyMs);
                monitor=new WasapiOut(monitorDevice,AudioClientShareMode.Shared,true,route.LatencyMs);
                monitor.Init(monitorBuffer); monitor.PlaybackStopped+=OnPlaybackStopped;
            }
            SetMonitorVolume(route.MonitorVolume);
            capture.DataAvailable+=OnData;
            capture.RecordingStopped+=OnCaptureStopped;
            running=true;
            output.Play(); monitor?.Play(); capture.StartRecording();
        }
        catch { Stop(); throw; }
    }

    private void OnPlaybackStopped(object? sender,StoppedEventArgs args)
    {
        if(!stopping && (ReferenceEquals(sender,output)||ReferenceEquals(sender,monitor))) { lastError=args.Exception?.Message ?? "Аудиовыход остановлен. Проверьте подключение устройства и запустите снова."; running=false; }
    }
    private void OnCaptureStopped(object? sender,StoppedEventArgs args)
    {
        if(!stopping && ReferenceEquals(sender,capture)) { lastError=args.Exception?.Message ?? "Микрофон отключён. Обновите список устройств и запустите снова."; running=false; }
    }

    private void OnData(object? sender,WaveInEventArgs args)
    {
        if(!running || capture==null) return;
        long begin=Stopwatch.GetTimestamp();
        try
        {
            lock(processingLock)
            {
                if(!running || processor==null || inputFormat==null || !ReferenceEquals(sender,capture)) return;
                var current=Volatile.Read(ref settings);
                if(!ReferenceEquals(current,applied)) { processor.Apply(current); applied=current; }
                processor.Bypass=bypass; processor.Muted=muted;
                WaveFormat format=inputFormat;
                int frames=args.BytesRecorded/format.BlockAlign;
                for(int start=0;start<frames;start+=decoded.Length)
                {
                    int length=Math.Min(decoded.Length,frames-start);
                    DecodeMono(args.Buffer,start,format,route.InputChannel,decoded.AsSpan(0,length));
                    int count=processor.Process(decoded.AsSpan(0,length),processed);
                    var samples=processed.AsSpan(0,count);
                    mainBuffer?.Add(samples);
                    if(monitorBuffer!=null)
                    {
                        for(int i=0;i<count;i++) monitorSamples[i]=processed[i]*monitorVolume;
                        monitorBuffer.Add(monitorSamples.AsSpan(0,count));
                    }
                    recording?.Add(samples);
                    if(count>0) for(int i=0;i<scope.Length;i++) scope[i]=processed[Math.Min(count-1,i*count/scope.Length)];
                }
                double elapsed=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
                dspLoad=dspLoad*.9+.1*elapsed/Math.Max(1,frames*1000.0/sampleRate)*100;
            }
        }
        catch(Exception ex) { lastError="Ошибка обработки аудио: "+ex.Message; running=false; }
    }

    internal static void DecodeMono(byte[] buffer,int start,WaveFormat format,int selected,Span<float> samples)
    {
        int bytes=format.BitsPerSample/8;
        int first=selected<0 ? 0 : Math.Min(selected,format.Channels-1);
        int channels=selected<0 ? format.Channels : 1;
        for(int i=0;i<samples.Length;i++) {
            int offset=(start+i)*format.BlockAlign+first*bytes;
            float total=0;
            for(int ch=0;ch<channels;ch++) total+=Decode(buffer,offset+ch*bytes,format.Encoding,format.BitsPerSample);
            samples[i]=total/channels;
        }
    }

    internal static float Decode(byte[] buffer,int offset,WaveFormatEncoding encoding,int bits)
    {
        if(encoding==WaveFormatEncoding.IeeeFloat) return BitConverter.ToSingle(buffer,offset);
        return bits switch
        {
            16=>BitConverter.ToInt16(buffer,offset)/32768f,
            24=>((buffer[offset]|buffer[offset+1]<<8|buffer[offset+2]<<16)<<8>>8)/8388608f,
            32=>BitConverter.ToInt32(buffer,offset)/2147483648f,
            _=>0
        };
    }

    public object MeterSnapshot()
    {
        lock(processingLock)
        {
            return new
            {
                running,bypass,muted,recording=recording!=null,
                recordingSeconds=recording?.Seconds ?? 0,
                recordingError=recording?.Error,
                inputDb=VoiceProcessor.LinearToDb(running ? processor?.InputPeak ?? 0 : 0),
                outputDb=VoiceProcessor.LinearToDb(running ? processor?.OutputPeak ?? 0 : 0),
                inputRmsDb=VoiceProcessor.LinearToDb(running ? processor?.InputRms ?? 0 : 0),
                gainReduction=processor?.GainReductionDb ?? 0,
                gateOpen=processor?.GateOpen ?? false,
                clippedSamples=processor?.ClippedInputSamples ?? 0,
                underruns=mainBuffer?.Underruns ?? 0, overruns=mainBuffer?.Overruns ?? 0,
                bufferedMs=Math.Round(mainBuffer?.BufferedMilliseconds ?? 0),
                dspLoad=Math.Round(dspLoad,1),sampleRate,
                estimatedLatencyMs=running ? Math.Round((processor?.AlgorithmLatencyMs ?? 0)+(mainBuffer?.BufferedMilliseconds ?? 0)+route.LatencyMs*2) : 0,
                waveform=running ? (float[])scope.Clone() : new float[128],
                error=lastError
            };
        }
    }

    public void StartRecording(string path)
    {
        lock(lifecycleLock)
        {
            if(!running) throw new InvalidOperationException("Сначала запустите обработку микрофона.");
            lock(processingLock) { if(recording!=null) return; }
            // Opening a WAV on a slow drive must not occupy the callback's lock.
            var session=new RecordingSession(path,sampleRate);
            lock(processingLock) {
                if(running) { recording=session; LastRecordingPath=path; return; }
            }
            session.Dispose();
            throw new InvalidOperationException("Аудиоустройство отключилось до начала записи.");
        }
    }
    public string? StopRecording()
    {
        lock(lifecycleLock) {
            RecordingSession? session;
            lock(processingLock) { session=recording; recording=null; }
            if(session==null) return null;
            session.Dispose(); if(session.Error!=null) throw new IOException(session.Error);
            return session.Path;
        }
    }
    public void Stop()
    {
        lock(lifecycleLock) StopCore();
    }
    private void StopCore()
    {
        stopping=true; running=false;
        void Cleanup(Action action) { try { action(); } catch(Exception ex) { lastError="Не удалось освободить аудиоустройство: "+ex.Message; } }
        if(capture!=null) { capture.DataAvailable-=OnData; capture.RecordingStopped-=OnCaptureStopped; Cleanup(capture.StopRecording); Cleanup(capture.Dispose); capture=null; }
        if(output!=null) { output.PlaybackStopped-=OnPlaybackStopped; Cleanup(output.Stop); Cleanup(output.Dispose); output=null; }
        if(monitor!=null) { monitor.PlaybackStopped-=OnPlaybackStopped; Cleanup(monitor.Stop); Cleanup(monitor.Dispose); monitor=null; }
        try { StopRecording(); } catch(Exception ex) { lastError=ex.Message; }
        lock(processingLock) { processor=null; mainBuffer=null; monitorBuffer=null; }
        if(inputDevice!=null) Cleanup(inputDevice.Dispose);
        if(outputDevice!=null) Cleanup(outputDevice.Dispose);
        if(monitorDevice!=null) Cleanup(monitorDevice.Dispose);
        inputDevice=null; outputDevice=null; monitorDevice=null;
    }
    public void Dispose() { lock(lifecycleLock) { disposed=true; StopCore(); } }
}
