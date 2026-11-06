using System.IO;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoxLab.Core;
using VoxLab.App.Audio;

namespace VoxLab.App;
internal static class Diagnostics
{
    public static Task Run(string[] args)=>Task.Run(async()=>
    {
        object report;
        try
        {
            var inputs=AudioEngine.Devices(DataFlow.Capture); var outputs=AudioEngine.Devices(DataFlow.Render);
            object? audio=null;
            if(args.Contains("--audio-smoke"))
            {
                using var engine=new AudioEngine { Muted=true };
                var input=inputs.FirstOrDefault(x=>x.IsDefault) ?? inputs.FirstOrDefault();
                var output=outputs.FirstOrDefault(x=>x.IsDefault) ?? outputs.FirstOrDefault();
                if(input==null || output==null) throw new InvalidOperationException("No capture/render endpoint available.");
                bool useRoute=args.Contains("--profile-route");
                var profile=ProfileStore.Load();
                var route=useRoute?profile.Route:new RouteSettings(input.Id,output.Id);
                if(useRoute) engine.Update(profile.Settings,profile.StudioProcessing);
                engine.Start(route);
                string testWav=Path.Combine(AppPaths.Data,"muted-recording-test.wav");
                engine.StartRecording(testWav);
                if(useRoute) await Task.Delay(20000);
                else {
                    await Task.Delay(1000);
                    engine.Update(PresetLibrary.All.First(p=>p.Id=="feminine").Settings);
                    await Task.Delay(1000);
                    engine.Update(PresetLibrary.All.First(p=>p.Id=="robot").Settings);
                    await Task.Delay(1000);
                }
                var metrics=engine.MeterSnapshot(); engine.StopRecording(); engine.Stop();
                using var reader=new WaveFileReader(testWav);
                bool silent=true;var samples=reader.ToSampleProvider();var buffer=new float[4096];int count;
                while((count=samples.Read(buffer,0,buffer.Length))>0) if(buffer.Take(count).Any(x=>x!=0)) silent=false;
                audio=new { input=inputs.FirstOrDefault(x=>x.Id==route.InputId)?.Name,output=outputs.FirstOrDefault(x=>x.Id==route.OutputId)?.Name,metrics,recording=new { seconds=reader.TotalTime.TotalSeconds,format=reader.WaveFormat.ToString(),silent,valid=reader.TotalTime.TotalSeconds>(useRoute?18:2) && silent } };
            }
            report=new { ok=true,inputs,outputs,audio };
        }
        catch(Exception ex) { report=new { ok=false,error=ex.ToString() }; }
        File.WriteAllText(Path.Combine(AppPaths.Data,"diagnostics.json"),JsonSerializer.Serialize(report,ProfileStore.Json));
    });
}
