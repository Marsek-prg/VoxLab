using System.Diagnostics;
using System.Text.Json;
using VoxLab.Core;

if(args.Length==2 && args[0]=="--export-presets")
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    File.WriteAllText(args[1],JsonSerializer.Serialize(PresetLibrary.All,new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true }));
    Console.WriteLine("Exported "+PresetLibrary.All.Count+" presets."); return;
}

int failures=0,checks=0;
void Check(string name,Action action)
{
    checks++;try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}
}
void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
const int Rate=48000;
VoiceSettings Neutral()=>new() { GateEnabled=false,CompressorEnabled=false,DeEsser=0,HighpassHz=20,OutputGain=0 };
float[] Sine(double frequency,int seconds=2,float amplitude=.2f)=>Enumerable.Range(0,Rate*seconds).Select(i=>(float)(amplitude*Math.Sin(2*Math.PI*frequency*i/Rate))).ToArray();
float[] Process(VoiceSettings settings,float[] signal,bool bypass=false,bool muted=false,int chunkSize=480)
{
    var processor=new VoiceProcessor(Rate) { Bypass=bypass,Muted=muted };processor.Apply(settings);
    var result=new List<float>(signal.Length+32768);var buffer=new float[32768];
    for(int i=0;i<signal.Length;i+=chunkSize)
    {
        int n=processor.Process(signal.AsSpan(i,Math.Min(chunkSize,signal.Length-i)),buffer);
        result.AddRange(buffer.AsSpan(0,n).ToArray());
    }
    int tail=processor.Flush(buffer);result.AddRange(buffer.AsSpan(0,tail).ToArray());return result.ToArray();
}
double Rms(IEnumerable<float> values){var a=values.ToArray();return Math.Sqrt(a.Sum(v=>(double)v*v)/Math.Max(1,a.Length));}
double Frequency(float[] values)
{
    int start=Rate/2,end=Math.Min(values.Length,Rate*3/2),crossings=0;
    for(int i=start+1;i<end;i++)if(values[i-1]<=0&&values[i]>0)crossings++;
    return crossings*Rate/(double)(end-start);
}

Check("All presets have unique IDs and valid bounded parameters",()=>{
    Assert(PresetLibrary.All.Select(p=>p.Id).Distinct().Count()==PresetLibrary.All.Count,"Duplicate preset id");
    foreach(var preset in PresetLibrary.All)Assert(preset.Settings==preset.Settings.Validate(),"Invalid preset: "+preset.Name);
});
Check("Untrusted numeric input is sanitized",()=>{
    var s=(new VoiceSettings { Pitch=double.NaN,InputGain=double.PositiveInfinity,EchoFeedback=999,CompressorRatio=-1,OutputGain=-999 }).Validate();
    Assert(double.IsFinite(s.Pitch)&&s.EchoFeedback==70&&s.CompressorRatio==1&&s.OutputGain==-36,"Parameter clamp failed");
});
Check("Clean processing preserves 220 Hz pitch",()=>{
    double f=Frequency(Process(Neutral(),Sine(220)));Assert(Math.Abs(f-220)<3,"Measured "+f+" Hz");
});
foreach(double semitones in new[]{-12d,-7d,4d,7d,12d})
{
    double shift=semitones;
    Check("Pitch shift "+shift+" semitones at unchanged duration",()=>{
        var input=Sine(220,3);var output=Process(Neutral() with { Pitch=shift },input);
        double expected=220*Math.Pow(2,shift/12),measured=Frequency(output);
        Assert(Math.Abs(measured-expected)/expected<.025,$"Expected {expected:F1}, got {measured:F1} Hz");
        Assert(Math.Abs(output.Length-input.Length)<Rate*.15,$"Duration drift: {output.Length-input.Length} samples");
    });
}
Check("Every character produces finite bounded audio",()=>{
    var signal=Sine(220);
    foreach(var p in PresetLibrary.All)
    {
        var audio=Process(p.Settings,signal);
        Assert(audio.Length>Rate,"Empty stream: "+p.Id);
        Assert(audio.All(v=>float.IsFinite(v)&&Math.Abs(v)<=.950001),"Unsafe sample: "+p.Id);
        Assert(Rms(audio.Skip(Rate/2))>.0001,"Silent preset: "+p.Id);
    }
});
Check("Limiter catches high-gain and hostile samples",()=>{
    var signal=Sine(350,2,4);signal[150]=float.NaN;signal[320]=float.PositiveInfinity;
    var output=Process(Neutral() with { InputGain=24,OutputGain=12,Drive=100,ReverbMix=70,EchoMix=70 },signal);
    Assert(output.All(v=>float.IsFinite(v)&&Math.Abs(v)<=.950001),"Limiter ceiling violated");
});
Check("Mute is absolute, including effect tails",()=>{
    var output=Process(Neutral() with { EchoMix=70,ReverbMix=70,Pitch=5 },Sine(240),muted:true);
    Assert(output.All(v=>v==0),"Mute leaked samples");
});
Check("Bypass removes pitch and character effects",()=>{
    var output=Process(Neutral() with { Pitch=12,RobotMix=100,Radio=true },Sine(220),bypass:true);
    Assert(Math.Abs(Frequency(output)-220)<3,"Bypass altered pitch");
});
Check("Gate attenuates quiet background after hold",()=>{
    var s=Neutral() with { GateEnabled=true,GateThreshold=-35,GateReleaseMs=40,GateHoldMs=20 };
    var output=Process(s,Sine(180,2,.002f));
    Assert(Rms(output.Skip(Rate))<.00001,"Gate did not close");
});
Check("Gate passes speech above threshold",()=>{
    var output=Process(Neutral() with { GateEnabled=true,GateThreshold=-40 },Sine(220,2,.2f));
    Assert(Rms(output.Skip(Rate))>.1,"Gate swallowed voice");
});
Check("Compressor reduces steady-state level",()=>{
    var signal=Sine(250,3,.7f);
    double dry=Rms(Process(Neutral(),signal).Skip(Rate));
    double compressed=Rms(Process(Neutral() with { CompressorEnabled=true,CompressorThreshold=-25,CompressorRatio=6,CompressorMakeup=0 },signal).Skip(Rate));
    Assert(compressed<dry*.5,$"Insufficient gain reduction: {compressed/dry}");
});
Check("Echo creates the requested delayed impulse",()=>{
    var signal=new float[Rate];signal[Rate/10]=.5f;
    var output=Process(Neutral() with { EchoMix=50,EchoDelayMs=200,EchoFeedback=0 },signal,bypass:false);
    int target=Rate*3/10;double peak=output.Skip(target-10).Take(20).Max(v=>Math.Abs(v));
    Assert(peak>.12,"Delayed impulse missing");
});
Check("Radio rejects sub-bass",()=>{
    var signal=Sine(60);double dry=Rms(Process(Neutral(),signal).Skip(Rate));
    double filtered=Rms(Process(Neutral() with { Radio=true },signal).Skip(Rate));
    Assert(filtered<dry*.12,"Radio high pass failed");
});
Check("Changing parameters mid-stream stays finite",()=>{
    var p=new VoiceProcessor(Rate);var input=Sine(200);var output=new float[32768];
    for(int n=0;n<200;n++)
    {
        p.Apply(PresetLibrary.All[n%PresetLibrary.All.Count].Settings);p.Bypass=n%11==0;
        int count=p.Process(input.AsSpan(0,480),output);
        Assert(output.AsSpan(0,count).ToArray().All(v=>float.IsFinite(v)&&Math.Abs(v)<=.950001),"Unstable parameter switch");
    }
});
Check("Silence does not generate noise or non-finite values",()=>{
    foreach(var preset in PresetLibrary.All)
        Assert(Process(preset.Settings,new float[Rate]).All(v=>v==0),"Silence not silent: "+preset.Id);
});
Check("44100/96000 Hz and irregular blocks are supported",()=>{
    foreach(int rate in new[]{44100,96000})
    {
        var p=new VoiceProcessor(rate);p.Apply(PresetLibrary.All[6].Settings);var input=new float[137];var output=new float[32768];
        for(int i=0;i<800;i++){for(int j=0;j<input.Length;j++)input[j]=(float)Math.Sin((i*137+j)*2*Math.PI*230/rate)*.2f;int n=p.Process(input,output);Assert(output.AsSpan(0,n).ToArray().All(float.IsFinite),"Non-finite audio at "+rate);}
    }
});
Check("Full DSP completes faster than signal duration",()=>{
    var signal=Sine(220,3);var stopwatch=Stopwatch.StartNew();Process(Neutral() with { Pitch=7,RobotMix=30,EchoMix=30,ChorusMix=50,ReverbMix=30 },signal);stopwatch.Stop();
    Console.WriteLine($"  3.0 s audio processed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms (offline benchmark)");
    Assert(stopwatch.Elapsed.TotalSeconds<3,"DSP slower than realtime on this machine");
});
Check("Steady-state DSP has bounded allocation over 20 seconds",()=>{
    var processor=new VoiceProcessor(Rate);processor.Apply(Neutral() with { Pitch=7,RobotMix=30,EchoMix=30,ChorusMix=50,ReverbMix=30 });
    var input=Sine(220).AsSpan(0,480).ToArray();var output=new float[32768];
    for(int i=0;i<300;i++)processor.Process(input,output);
    long before=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
    for(int i=0;i<2000;i++)processor.Process(input,output);
    clock.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
    Console.WriteLine($"  20.0 s streaming DSP: {clock.Elapsed.TotalMilliseconds:F0} ms, {allocated} allocated bytes");
    Assert(allocated<262144,"Steady-state allocations exceed 256 KiB");
});
Check("Old profiles retain full strength and new numeric inputs are bounded",()=>{
    var old=JsonSerializer.Deserialize<VoiceSettings>("{\"pitch\":4}",new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Assert(old.EffectStrength==100 && old.CrushMix==0 && old.RadioAmount==100,"Old profile changed semantics");
    var hostile=(old with { EffectStrength=999,CrushBits=-5,CrushRate=double.NaN,TremoloRate=100,FlangerRate=-10 }).Validate();
    Assert(hostile.EffectStrength==200 && hostile.CrushBits==3 && double.IsFinite(hostile.CrushRate) && hostile.TremoloRate==30 && hostile.FlangerRate==.05,"Unsafe new parameter");
});
Check("Zero strength removes character while preserving cleanup and gains",()=>{
    var source=Neutral() with { EffectStrength=0,Pitch=8,Tone=50,Radio=true,RobotMix=80,Drive=80,CrushMix=100,TremoloMix=100,FlangerMix=100,EchoMix=60,ReverbMix=60,ChorusMix=100 };
    var effective=source.Effective();
    Assert(effective.Pitch==0 && effective.RadioAmount==0 && effective.CrushMix==0 && effective.FlangerMix==0 && effective.InputGain==source.InputGain && effective.CompressorEnabled==source.CompressorEnabled,"Strength includes cleanup");
    Assert(Process(source,Sine(300)).SequenceEqual(Process(Neutral(),Sine(300))),"Zero strength left a character effect");
});
Check("Double strength stays bounded for every preset",()=>{
    foreach(var p in PresetLibrary.All) {
        var audio=Process(p.Settings with { EffectStrength=200 },Sine(700,1));
        Assert(audio.All(v=>float.IsFinite(v)&&Math.Abs(v)<=.950001),"Unsafe boosted preset: "+p.Id);
    }
});
Check("Radio is narrow with clear mids and rejects treble",()=>{
    var radio=Neutral() with { Radio=true,RadioAmount=100 };
    double mid=Rms(Process(radio,Sine(1300)).Skip(Rate));
    double low=Rms(Process(radio,Sine(80)).Skip(Rate));
    double high=Rms(Process(radio,Sine(6500)).Skip(Rate));
    Assert(mid>.05 && low<mid*.03 && high<mid*.08,$"Bad radio band: low={low}, mid={mid}, high={high}");
});
Check("New modulation effects are independent of audio block boundaries",()=>{
    var fx=Neutral() with { CrushMix=75,CrushBits=5,CrushRate=5500,TremoloMix=90,TremoloRate=11.3,FlangerMix=80,FlangerRate=.37 };
    var signal=Sine(730,2);var a=Process(fx,signal,chunkSize:137);var b=Process(fx,signal,chunkSize:480);
    Assert(a.SequenceEqual(b),"Modulation depends on driver block size");
    Assert(!a.SequenceEqual(Process(Neutral(),signal)),"Effects had no effect");
});
Check("All new effects stream with bounded allocation and CPU time",()=>{
    var processor=new VoiceProcessor(Rate);processor.Apply(Neutral() with { Radio=true,CrushMix=80,TremoloMix=80,FlangerMix=80,Pitch=-5,RobotMix=70,ReverbMix=50,EchoMix=40,ChorusMix=40 });
    var input=Sine(500).AsSpan(0,480).ToArray();var output=new float[32768];
    for(int i=0;i<300;i++)processor.Process(input,output);
    long before=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
    for(int i=0;i<2000;i++)processor.Process(input,output);
    clock.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
    Console.WriteLine($"  New FX / 20 s audio: {clock.Elapsed.TotalMilliseconds:F0} ms, {allocated} bytes");
    Assert(allocated<262144 && clock.Elapsed.TotalSeconds<10,"New FX too costly");
});
Console.WriteLine($"\n{checks-failures}/{checks} checks passed.");Environment.ExitCode=failures>0?1:0;
