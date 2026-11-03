using System.Runtime.InteropServices;
using System.IO;
using NAudio.Wave;
using VoxLab.App.Audio;

int failures=0,checks=0;
void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
void Check(string name,Action test) { checks++; try { test(); Console.WriteLine("PASS "+name); } catch(Exception ex) { failures++; Console.WriteLine("FAIL "+name+": "+ex.Message); } }
float[] Read(AudioRingBuffer buffer,int count) { var bytes=new byte[count*4]; buffer.Read(bytes,0,bytes.Length); return MemoryMarshal.Cast<byte,float>(bytes).ToArray(); }
Check("Ring buffer prebuffers and never exposes stale bytes",()=> {
    var ring=new AudioRingBuffer(8000,20);ring.Add(Enumerable.Repeat(.25f,100).ToArray());
    Assert(Read(ring,80).All(v=>v==0),"Played before prebuffer");
    ring.Add(Enumerable.Repeat(.5f,60).ToArray());
    Assert(Read(ring,160).SequenceEqual(Enumerable.Repeat(.25f,100).Concat(Enumerable.Repeat(.5f,60))),"Ordering changed");
    Assert(Read(ring,80).All(v=>v==0) && ring.Underruns==1,"Starvation not counted/silent");
});
Check("Ring buffer wrap and oversized blocks keep newest half second",()=> {
    var ring=new AudioRingBuffer(8000,20);
    ring.Add(Enumerable.Range(0,3900).Select(i=>(float)i).ToArray());Read(ring,3800);
    ring.Add(Enumerable.Range(3900,500).Select(i=>(float)i).ToArray());
    Assert(Read(ring,600).SequenceEqual(Enumerable.Range(3800,600).Select(i=>(float)i)),"Wrap corrupts audio");
    ring.Add(Enumerable.Range(0,6000).Select(i=>(float)i).ToArray());
    Assert(Read(ring,4000).SequenceEqual(Enumerable.Range(2000,4000).Select(i=>(float)i)),"Oversize failed");
    Assert(ring.Overruns>0 && ring.BufferedMilliseconds==0,"Queue is unbounded");
});
Check("Ring buffer preserves caller prefix and suffix",()=> {
    var ring=new AudioRingBuffer(8000,20);ring.Add(new float[160]);
    var bytes=Enumerable.Repeat((byte)0xA5,660).ToArray();ring.Read(bytes,8,640);
    Assert(bytes.Take(8).All(v=>v==0xA5) && bytes.Skip(648).All(v=>v==0xA5),"Write outside requested span");
});
Check("Concurrent audio producer/consumer stay bounded",()=> {
    var ring=new AudioRingBuffer(48000,20);var block=Enumerable.Repeat(.2f,480).ToArray();
    Parallel.Invoke(()=>{for(int i=0;i<10000;i++)ring.Add(block);},()=>{var bytes=new byte[1920];for(int i=0;i<10000;i++)ring.Read(bytes,0,bytes.Length);});
    Assert(ring.BufferedMilliseconds<=500,"Queue grew beyond half second");
});
Check("WAV writer finalizes exact floating point samples",()=> {
    string path=Path.Combine(Path.GetTempPath(),"VoxLab-wav-"+Guid.NewGuid()+".wav");
    try {
        var values=Enumerable.Range(0,480).Select(i=>(float)Math.Sin(i*.03)*.3f).ToArray();
        using(var recording=new RecordingSession(path,48000)) for(int i=0;i<100;i++)recording.Add(values);
        using var reader=new WaveFileReader(path);var samples=reader.ToSampleProvider();var actual=new float[48000];int total=0,n;
        while(total<actual.Length && (n=samples.Read(actual,total,actual.Length-total))>0)total+=n;
        Assert(total==48000 && Math.Abs(reader.TotalTime.TotalSeconds-1)<.001,"Bad duration/header");
        for(int i=0;i<actual.Length;i++)Assert(actual[i]==values[i%480],"Sample corruption");
    } finally { File.Delete(path); }
});
Check("Disk open failure is immediate and explicit",()=> {
    string path=Path.Combine(Path.GetTempPath(),"VoxLab-missing-"+Guid.NewGuid(),"x.wav");bool rejected=false;
    try { using var recording=new RecordingSession(path,48000); } catch(DirectoryNotFoundException) { rejected=true; }
    Assert(rejected,"Invalid destination was accepted");
});
Check("Repeated stop/dispose is safe and disposed engine cannot start",()=> {
    var engine=new AudioEngine();engine.Stop();engine.Stop();engine.Dispose();engine.Dispose();bool rejected=false;
    try { engine.Start(new RouteSettings()); } catch(ObjectDisposedException) { rejected=true; }
    Assert(rejected,"Disposed engine can restart");
});
Check("Missing endpoints fail before capturing",()=> {
    using var engine=new AudioEngine();bool rejected=false;
    try { engine.Start(new RouteSettings()); } catch(InvalidOperationException) { rejected=true; }
    Assert(rejected && !engine.IsRunning && !engine.IsRecording,"Invalid route captured audio");
});
Console.WriteLine($"\n{checks-failures}/{checks} Windows checks passed.");Environment.ExitCode=failures>0?1:0;
