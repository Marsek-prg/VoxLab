using System.Runtime.InteropServices;
using System.IO;
using NAudio.Wave;
using VoxLab.App.Audio;
using VoxLab.App;
using VoxLab.Core;
using System.IO.Compression;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;

// Explicit release verification: operate only on a caller-provided disposable
// portable copy, then let the actual helper replace files and restart that copy.
if(args.Length==4 && args[0]=="--live-update-smoke")
{
    using var updater=new UpdateService(installedVersion:Version.Parse(args[3]));
    updater.Changed+=status=>Console.WriteLine(status.Phase+": "+status.Message);
    await updater.CheckAsync(Path.GetFullPath(args[1]));
    if(updater.Status.Phase!="available") throw new Exception(updater.Status.Message);
    await updater.PrepareAsync(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]));
    if(updater.Status.Phase!="ready") throw new Exception(updater.Status.Message);
    File.WriteAllText(Path.Combine(args[2],"live-update-smoke.json"),JsonSerializer.Serialize(updater.Status));
    await updater.LaunchInstallerAsync();
    return;
}

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
Check("Hotkeys migrate old profiles and preserve an intentionally empty list",()=> {
    var old=JsonSerializer.Deserialize<UserProfile>("{}")!;
    Assert(old.HotkeyBindings.Count==11,"Old shortcuts lost");
    var empty=JsonSerializer.Deserialize<UserProfile>("{\"HotkeyBindings\":[]}")!;
    Assert(empty.HotkeyBindings.Count==0,"Empty shortcuts replaced");
    Hotkeys.Validate(old.HotkeyBindings,PresetLibrary.All.Select(p=>p.Id));
});
Check("Hotkeys reject duplicates, reserved keys and missing preset targets",()=> {
    foreach(var list in new List<HotkeyBinding>[] {
        [new(77,3,"mute"),new(77,3,"bypass")], [new(77,0,"mute")], [new(123,3,"mute")],
        [new(115,1,"mute")], [new(65,8,"mute")], [new(65,3,"preset","missing")], [new(65,3,"unknown")] }) {
        bool rejected=false;try { Hotkeys.Validate(list,PresetLibrary.All.Select(p=>p.Id)); } catch(ArgumentException) { rejected=true; }
        Assert(rejected,"Unsafe shortcut accepted");
    }
    Hotkeys.Validate([new(112,0,"mute"),new(39,6,"next"),new(32,3,"record")],[]);
});
Check("Updater rejects traversal, Data, device names, alternate streams and collisions",()=> {
    foreach(string path in new[]{"../VoxLab.exe","App/../Data/profile.json","Data/profile.json","App/x:stream","App/a\\b","App/CON.txt","App/foo. ","/App/x","App/.update.lock","App//x"})
        Assert(!UpdateService.SafeRelativePath(path),"Accepted "+path);
    Assert(UpdateService.SafeRelativePath("App/Web/fonts/Manrope.woff2"),"Valid file rejected");
    string hash=new('a',64);
    var manifest=new UpdateManifest(1,"0.2.0",[new("VoxLab.exe",hash,1,"app"),new("App/VoxLab.dll",hash,1,"app")],[new("app","VoxLab-app-123.zip",hash,100)]);
    UpdateService.ValidateManifest(manifest,"0.2.0");
    foreach(var invalid in new[]{manifest with { Format=2 },manifest with { Version="1.0.0" },manifest with { Files=[..manifest.Files,new("App/VOXLAB.dll",hash,1,"app")] },manifest with { Files=[..manifest.Files,new("App/Web",hash,1,"app"),new("App/Web/x.js",hash,1,"app")] }}) {
        bool rejected=false;try { UpdateService.ValidateManifest(invalid,"0.2.0"); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"Invalid manifest accepted");
    }
});
Check("Incremental updater hashes files, extracts only changes and rejects corrupted ZIPs",()=> {
    string root=Path.Combine(Path.GetTempPath(),"VoxLab-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    try {
        string installed=Path.Combine(root,"installed"),payload=Path.Combine(root,"payload"),zip=Path.Combine(root,"bundle.zip");
        Directory.CreateDirectory(Path.Combine(installed,"App"));File.WriteAllText(Path.Combine(installed,"VoxLab.exe"),"same");File.WriteAllText(Path.Combine(installed,"App","VoxLab.dll"),"old!");
        string source=Path.Combine(root,"new.dll");File.WriteAllText(source,"new!");
        var stable=new UpdateFile("VoxLab.exe",UpdateService.Hash(Path.Combine(installed,"VoxLab.exe")),4,"runtime");
        var update=new UpdateFile("App/VoxLab.dll",UpdateService.Hash(source),4,"app");
        var manifest=new UpdateManifest(1,"0.2.0",[stable,update],[]);
        var changes=UpdateService.FindChanges(installed,manifest);Assert(changes.Count==1 && changes[0]==update,"Redownloads unchanged components / misses same-size change");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))archive.CreateEntryFromFile(source,"App/VoxLab.dll");
        UpdateService.ExtractBundle(zip,payload,manifest,"app",changes);
        Assert(File.ReadAllText(Path.Combine(payload,"App","VoxLab.dll"))=="new!" && !File.Exists(Path.Combine(payload,"VoxLab.exe")),"Wrong extracted payload");
        File.Delete(zip);using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))archive.CreateEntryFromFile(Path.Combine(installed,"App","VoxLab.dll"),"App/VoxLab.dll");
        bool rejected=false;try { UpdateService.ExtractBundle(zip,payload,manifest,"app",changes); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"Corrupted update accepted");
        File.Delete(zip);using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))archive.CreateEntryFromFile(source,"../escape.dll");
        rejected=false;try { UpdateService.ExtractBundle(zip,payload,manifest,"app",changes); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected && !File.Exists(Path.Combine(root,"escape.dll")),"ZIP traversal accepted");
    } finally { Directory.Delete(root,true); }
});
Check("Update service downloads only changed components and prepares a verified restart plan",()=> {
    string root=Path.Combine(Path.GetTempPath(),"VoxLab-service-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"App"));
    try {
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        byte[] old=Encoding.UTF8.GetBytes("old"),fresh=Encoding.UTF8.GetBytes("new");
        string Digest(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
        File.WriteAllBytes(Path.Combine(root,"VoxLab.exe"),old);File.WriteAllBytes(Path.Combine(root,"App","VoxLab.dll"),old);
        byte[] Bundle(string path,byte[] bytes) {
            using var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))using(var entry=zip.CreateEntry(path).Open())entry.Write(bytes);
            return stream.ToArray();
        }
        byte[] app=Bundle("App/VoxLab.dll",fresh),runtime=Bundle("VoxLab.exe",old);
        var manifest=new UpdateManifest(1,"0.2.1",[new("VoxLab.exe",Digest(old),3,"runtime"),new("App/VoxLab.dll",Digest(fresh),3,"app")],
            [new("app","VoxLab-app-test.zip",Digest(app),app.Length),new("runtime","VoxLab-runtime-test.zip",Digest(runtime),runtime.Length)]);
        const string prefix="https://github.com/Marsek-prg/VoxLab/releases/download/v0.2.1/";
        var assets=new[]{"VoxLab-update.json","VoxLab-app-test.zip","VoxLab-runtime-test.zip"}.Select(name=>new {name,browser_download_url=prefix+name});
        var transport=new FakeUpdateTransport(new() {
            ["https://api.github.com/repos/Marsek-prg/VoxLab/releases/latest"]=JsonSerializer.SerializeToUtf8Bytes(new{tag_name="v0.2.1",prerelease=false,draft=false,assets},json),
            [prefix+"VoxLab-update.json"]=JsonSerializer.SerializeToUtf8Bytes(manifest,json),[prefix+"VoxLab-app-test.zip"]=app,[prefix+"VoxLab-runtime-test.zip"]=runtime
        });
        using var updater=new UpdateService(transport);
        updater.CheckAsync(root).GetAwaiter().GetResult();
        Assert(updater.Status.Phase=="available" && updater.Status.ChangedFiles==1 && updater.Status.DownloadBytes==app.Length,"Wrong incremental plan: "+updater.Status.Message);
        updater.PrepareAsync(root,Path.Combine(root,"Data")).GetAwaiter().GetResult();
        Assert(updater.Status.Phase=="ready","Prepare failed: "+updater.Status.Message);
        Assert(!transport.Requests.Contains(prefix+"VoxLab-runtime-test.zip"),"Unchanged runtime downloaded");
        Assert(File.ReadAllBytes(Path.Combine(root,"App","VoxLab.dll")).SequenceEqual(old),"Preparation modified a running install");
        string plan=Directory.GetFiles(Path.Combine(root,"Data"),"plan.json",SearchOption.AllDirectories).Single();
        using var parsed=JsonDocument.Parse(File.ReadAllText(plan));
        Assert(parsed.RootElement.GetProperty("files").GetArrayLength()==1 && File.Exists(Path.Combine(Path.GetDirectoryName(plan)!,"Update-VoxLab.ps1")),"Incomplete install plan");
    } finally { Directory.Delete(root,true); }
});
Check("Network failures and timeouts leave the updater retryable",()=> {
    foreach(bool timeout in new[]{false,true}) {
        using var updater=new UpdateService(new FakeUpdateTransport([],timeout));
        updater.CheckAsync(Path.GetTempPath()).GetAwaiter().GetResult();
        Assert(updater.Status.Phase=="error","Failed request left updater busy");
        updater.CheckAsync(Path.GetTempPath()).GetAwaiter().GetResult();
        Assert(updater.Status.Phase=="error","Retry could not run");
    }
});
Console.WriteLine($"\n{checks-failures}/{checks} Windows checks passed.");Environment.ExitCode=failures>0?1:0;

sealed class FakeUpdateTransport(Dictionary<string,byte[]> responses,bool timeout=false) : HttpMessageHandler
{
    public List<string> Requests { get; }=[];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        if(timeout) throw new TaskCanceledException("Simulated timeout");
        string url=request.RequestUri!.AbsoluteUri; Requests.Add(url);
        return Task.FromResult(responses.TryGetValue(url,out var data)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content=new ByteArrayContent(data) }
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
