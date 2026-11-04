using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoxLab.App.Audio;
using VoxLab.Core;

namespace VoxLab.App;
public partial class MainWindow
{
    // Explicit diagnostic mode only. No voice samples are saved: the entire
    // output, including the WAV tap, is forcibly muted throughout this run.
    private async Task RunPerformance()
    {
        var phases=new List<object>(); var checks=new List<object>();
        string? failure=null;
        Dictionary<int,(double Cpu,long Private,long Working)> Snapshot()
        {
            var result=new Dictionary<int,(double,long,long)>();
            var ids=Web.CoreWebView2.Environment.GetProcessInfos().Select(p=>p.ProcessId).Append(Environment.ProcessId).Distinct();
            foreach(int id in ids)
                try { using var p=Process.GetProcessById(id); result[id]=(p.TotalProcessorTime.TotalSeconds,p.PrivateMemorySize64,p.WorkingSet64); }
                catch(ArgumentException) { /* A WebView helper may have exited. */ }
            return result;
        }
        async Task Measure(string name,int seconds)
        {
            var before=Snapshot(); var watch=Stopwatch.StartNew();
            await Task.Delay(seconds*1000); watch.Stop();
            var after=Snapshot();
            double cpu=after.Where(p=>before.ContainsKey(p.Key)).Sum(p=>Math.Max(0,p.Value.Cpu-before[p.Key].Cpu));
            phases.Add(new { name,seconds=watch.Elapsed.TotalSeconds,processes=after.Count,
                totalMachineCpuPercent=cpu/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100,
                oneCoreCpuPercent=cpu/watch.Elapsed.TotalSeconds*100,
                privateMB=after.Values.Sum(v=>v.Private)/1048576.0,
                summedWorkingSetMB=after.Values.Sum(v=>v.Working)/1048576.0,
                metrics=engine.MeterSnapshot() });
        }
        void Check(string name,bool passed) { checks.Add(new { name,passed }); if(!passed) throw new InvalidOperationException(name); }
        try
        {
            await Task.Delay(5000);
            await Measure("visible-idle",10);
            var input=AudioEngine.Devices(DataFlow.Capture).FirstOrDefault(x=>x.IsDefault);
            var output=AudioEngine.Devices(DataFlow.Render).FirstOrDefault(x=>x.IsDefault);
            if(input==null || output==null) throw new InvalidOperationException("No default audio endpoints for performance run.");
            var route=new RouteSettings(input.Id,output.Id);
            engine.Muted=true;
            engine.Update(new VoiceSettings { Pitch=7,RobotMix=30,Drive=20,ChorusMix=50,EchoMix=30,ReverbMix=30,Radio=true,CrushMix=50,TremoloMix=50,FlangerMix=60 });
            await Task.Run(()=>engine.Start(route)); SendState();
            string wav=Path.Combine(AppPaths.Data,"performance-muted.wav");
            engine.StartRecording(wav);
            await Task.Delay(2000);
            await Measure("visible-all-effects-and-recording",20);
            engine.StopRecording();
            using(var reader=new WaveFileReader(wav))
            {
                var samples=reader.ToSampleProvider(); var block=new float[4096]; bool silent=true; int count;
                while((count=samples.Read(block,0,block.Length))>0) for(int i=0;i<count;i++) if(block[i]!=0) silent=false;
                Check("WAV finalized, >20 seconds, all samples muted",reader.TotalTime.TotalSeconds>20 && silent);
            }
            Hide(); await Task.Delay(2000);
            await Measure("tray-all-effects",20);
            RestoreWindow();
            for(int i=0;i<PresetLibrary.All.Count;i++)
            {
                engine.Update(PresetLibrary.All[i].Settings);
                engine.Bypass=i%5==0;
                await Task.Delay(1000);
                Check("Preset "+PresetLibrary.All[i].Id+" stays running",engine.IsRunning && engine.Error==null);
                if(i%4==3) { engine.Stop(); await Task.Run(()=>engine.Start(route)); }
            }
            engine.Bypass=false;
            await Measure($"visible-after-{PresetLibrary.All.Count}-presets-and-{PresetLibrary.All.Count/4}-restarts",10);
            engine.Stop(); SendState();
            await Measure("visible-stopped-after-stress",10);
            Check("Stop releases capture",!engine.IsRunning && !engine.IsRecording);
            bool rejected=false;
            try { engine.Start(new RouteSettings()); } catch(InvalidOperationException) { rejected=true; }
            Check("Invalid route rejected without starting audio",rejected && !engine.IsRunning);
        }
        catch(Exception ex) { failure=ex.ToString(); }
        finally
        {
            engine.Stop();
            File.WriteAllText(Path.Combine(AppPaths.Data,"performance.json"),JsonSerializer.Serialize(new {
                ok=failure==null,error=failure,logicalProcessors=Environment.ProcessorCount,phases,checks
            },ProfileStore.Json));
            Close();
        }
    }
}
