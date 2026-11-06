using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using NAudio.CoreAudioApi;
using VoxLab.App.Audio;
using VoxLab.Core;
using Forms=System.Windows.Forms;

namespace VoxLab.App;
public partial class MainWindow : Window
{
    private readonly AudioEngine engine=new();
    private UserProfile profile=ProfileStore.Load();
    private readonly DispatcherTimer meterTimer=new() { Interval=TimeSpan.FromMilliseconds(60) };
    private readonly DispatcherTimer saveTimer=new() { Interval=TimeSpan.FromMilliseconds(500) };
    private Forms.NotifyIcon? tray;
    private readonly string[] arguments;
    private readonly List<int> registeredKeys=[];
    private readonly List<string> hotkeyFailures=[];
    private readonly Dictionary<int,HotkeyBinding> keyActions=[];
    private readonly UpdateService updater=new();
    private CancellationTokenSource? checkOperation;
    private VoiceCheck? checkClip;
    private bool capturingHotkey;
    private bool ready, closing, busy, faultHandled;
    internal bool ExitRequested { get; set; }
    private IntPtr handle;

    public MainWindow(string[] args)
    {
        arguments=args; InitializeComponent();
        updater.Changed+=status=>Dispatcher.InvokeAsync(()=>Send("update",status));
        int sizeArgument=Array.IndexOf(args,"--size");
        if(sizeArgument>=0 && sizeArgument+1<args.Length)
        {
            var parts=args[sizeArgument+1].Split('x');
            if(parts.Length==2 && int.TryParse(parts[0],out int width) && int.TryParse(parts[1],out int height)) { Width=Math.Max(MinWidth,width); Height=Math.Max(MinHeight,height); }
        }
        Loaded+=async(_,_)=>await InitializeBrowser();
        IsVisibleChanged+=async(_,_)=>await UpdateVisualActivity();
        StateChanged+=async(_,_)=>await UpdateVisualActivity();
        Deactivated+=(_,_)=> { if(capturingHotkey) { capturingHotkey=false; ConfigureHotkeys(); Send("cancelKeyCapture",true); } };
        SourceInitialized+=(_,_)=>
        {
            handle=new WindowInteropHelper(this).Handle;
            int dark=1; DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
            ConfigureHotkeys();
        };
        Closing+=(_,e)=>
        {
            if(!ExitRequested && tray?.Visible==true) { e.Cancel=true; Hide(); return; }
            closing=true; CloseVoiceCheck(); meterTimer.Stop(); saveTimer.Stop();
            foreach(int id in registeredKeys) UnregisterHotKey(handle,id);
            engine.Dispose(); tray?.Dispose(); updater.Dispose();
            try { ProfileStore.Save(profile); } catch { }
            Web.Dispose();
        };
        meterTimer.Tick+=(_,_)=>
        {
            bool visible=IsVisible && WindowState!=WindowState.Minimized;
            meterTimer.Interval=TimeSpan.FromMilliseconds(engine.IsRunning ? visible ? 60 : 250 : 1000);
            if(visible && engine.IsRunning) Send("meter",engine.MeterSnapshot());
            if(engine.RecordingError!=null)
            {
                try { engine.StopRecording(); } catch(Exception ex) { Notify(ex.Message,true); }
                SendState();
            }
            if(engine.Error!=null && !engine.IsRunning && !faultHandled)
            {
                faultHandled=true; Notify(engine.Error,true); engine.Stop(); SendState();
            }
        };
        saveTimer.Tick+=(_,_)=> { saveTimer.Stop(); try { ProfileStore.Save(profile); } catch(Exception ex) { Notify("Настройки не сохранены: "+ex.Message,true); } };
        ApplyAudioSettings();
    }

    private async Task InitializeBrowser()
    {
        try
        {
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(AppPaths.Data,"WebView2"));
            await Web.EnsureCoreWebView2Async(environment);
            Web.CoreWebView2.SetVirtualHostNameToFolderMapping("voxlab.local",Path.Combine(AppContext.BaseDirectory,"Web"),CoreWebView2HostResourceAccessKind.DenyCors);
            Web.CoreWebView2.Settings.AreDevToolsEnabled=arguments.Contains("--dev");
            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
            Web.CoreWebView2.Settings.IsStatusBarEnabled=false;
            Web.CoreWebView2.Settings.IsZoomControlEnabled=true;
            Web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled=false;
            Web.CoreWebView2.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            Web.CoreWebView2.NewWindowRequested+=(_,e)=>e.Handled=true;
            Web.CoreWebView2.NavigationStarting+=(_,e)=> { if(!e.Uri.StartsWith("https://voxlab.local/",StringComparison.Ordinal)) e.Cancel=true; };
            Web.CoreWebView2.WebMessageReceived+=OnMessage;
            Web.CoreWebView2.NavigationCompleted+=(_,_)=>LoadingLabel.Visibility=Visibility.Collapsed;
            Web.Source=new Uri("https://voxlab.local/index.html");
            SetupTray();
        }
        catch(Exception ex)
        {
            LoadingLabel.Text="Не удалось открыть интерфейс VoxLab.\nНужен Microsoft Edge WebView2 Runtime.\n\n"+ex.Message;
            LoadingLabel.TextWrapping=TextWrapping.Wrap; LoadingLabel.Margin=new Thickness(48);
        }
    }

    private void SetupTray()
    {
        tray=new Forms.NotifyIcon { Text="VoxLab — голосовая мастерская", Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Visible=true };
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть VoxLab",null,(_,_)=>Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("Выключить / включить микрофон",null,(_,_)=>Dispatcher.Invoke(()=> { engine.Muted=!engine.Muted; SendState(); }));
        var finishRecording=menu.Items.Add("Завершить запись WAV",null,(_,_)=>Dispatcher.Invoke(()=> {
            try { engine.StopRecording(); } catch(Exception ex) { Notify(ex.Message,true); } SendState();
        }));
        menu.Opening+=(_,_)=>finishRecording.Visible=engine.IsRecording;
        menu.Items.Add("Остановить обработку",null,(_,_)=>Dispatcher.Invoke(()=> { engine.Stop(); SendState(); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выйти из VoxLab",null,(_,_)=>Dispatcher.Invoke(ExitApplication));
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(RestoreWindow);
    }
    internal void RestoreWindow() { Show(); if(WindowState==WindowState.Minimized) WindowState=WindowState.Normal; Activate(); }
    private void ExitApplication() { ExitRequested=true; Close(); }
    private void ApplyAudioSettings()=>engine.Update(profile.Settings,profile.StudioProcessing);

    private async void OnMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!e.Source.StartsWith("https://voxlab.local/",StringComparison.Ordinal) || e.WebMessageAsJson.Length>250_000) return;
        try
        {
            using var document=JsonDocument.Parse(e.WebMessageAsJson);
            var root=document.RootElement;
            string? action=root.GetProperty("action").GetString();
            JsonElement data=root.TryGetProperty("data",out var payload) ? payload : default;
            switch(action)
            {
                case "ready":
                    ready=true; RefreshDevices(true); SendState(); Send("update",updater.Status); ShowUpdateResult(); meterTimer.Start();
                    if(arguments.Contains("--smoke")) await RunSmoke();
                    else if(arguments.Contains("--performance")) await RunPerformance();
                    break;
                case "settings":
                    string editId=data.GetProperty("presetId").GetString()!;
                    var settings=data.GetProperty("settings").Deserialize<VoiceSettings>(ProfileStore.Json) ?? new();
                    profile=profile.Remember(editId,settings); if(editId==profile.PresetId) ApplyAudioSettings(); SaveSoon();
                    Send("settingsAck",new { presetId=editId, sequence=data.GetProperty("sequence").GetInt64() }); break;
                case "resetPreset": ApplyPreset(profile.PresetId,true); break;
                case "checkCapture": case "checkOriginal": case "checkProcessed":
                    await RunVoiceCheck(action); break;
                case "checkClose": CloseVoiceCheck(); break;
                case "studioProcessing":
                    profile=profile with { StudioProcessing=data.GetBoolean() }; ApplyAudioSettings(); SaveSoon(); SendState(); break;
                case "preset": ApplyPreset(data.GetString()!); break;
                case "start": await StartEngine(); break;
                case "stop": engine.Stop(); SendState(); break;
                case "mute": engine.Muted=!engine.Muted; SendState(); break;
                case "bypass": engine.Bypass=!engine.Bypass; SendState(); break;
                case "refreshDevices": RefreshDevices(false); break;
                case "route":
                    if(engine.IsRunning) throw new InvalidOperationException("Остановите обработку, затем измените устройства.");
                    profile=profile with { Route=data.Deserialize<RouteSettings>(ProfileStore.Json) ?? new() }; SaveSoon(); SendState(); break;
                case "monitorVolume":
                    var volume=Math.Clamp(data.GetDouble(),0,100);
                    profile=profile with { Route=profile.Route with { MonitorVolume=volume } }; engine.SetMonitorVolume(volume); SaveSoon(); break;
                case "record": ToggleRecording(); break;
                case "savePreset": SavePreset(data.GetString()); break;
                case "deletePreset":
                    string? id=data.GetString(); profile.CustomPresets.RemoveAll(x=>x.Id==id); profile.Favorites.RemoveAll(x=>x==id);
                    if(id!=null) profile.VoiceEdits.Remove(id);
                    profile.HotkeyBindings.RemoveAll(x=>x.Action=="preset" && x.PresetId==id); ConfigureHotkeys();
                    if(profile.PresetId==id) ApplyPreset("studio"); SaveSoon(); SendState(); break;
                case "favorite":
                    var favorite=data.GetString()!; if(!profile.Favorites.Remove(favorite)) profile.Favorites.Add(favorite); SaveSoon(); SendState(); break;
                case "exportPreset": ExportPreset(); break;
                case "importPreset": ImportPreset(); break;
                case "hotkeys": profile=profile with { HotkeysEnabled=data.GetBoolean() }; ConfigureHotkeys(); SaveSoon(); SendState(); break;
                case "captureHotkey": capturingHotkey=data.GetBoolean(); ConfigureHotkeys(); break;
                case "saveHotkeys":
                    var bindings=data.Deserialize<List<HotkeyBinding>>(ProfileStore.Json) ?? [];
                    Hotkeys.Validate(bindings,PresetLibrary.All.Concat(profile.CustomPresets).Select(p=>p.Id));
                    profile=profile with { HotkeyBindings=bindings }; capturingHotkey=false; ConfigureHotkeys(); SaveSoon(); SendState(); Send("hotkeysSaved",true); break;
                case "resetHotkeys":
                    profile=profile with { HotkeyBindings=Hotkeys.Defaults() }; capturingHotkey=false; ConfigureHotkeys(); SaveSoon(); SendState(); Send("hotkeysSaved",true); break;
                case "checkUpdate": await updater.CheckAsync(AppPaths.Root); break;
                case "downloadUpdate": await updater.PrepareAsync(AppPaths.Root,AppPaths.Data); break;
                case "installUpdate":
                    if(engine.IsRecording || busy) throw new InvalidOperationException("Завершите запись и дождитесь запуска звука перед обновлением.");
                    ProfileStore.Save(profile); engine.Stop(); SendState();
                    await updater.LaunchInstallerAsync(); ExitApplication(); break;
                case "releases": Process.Start(new ProcessStartInfo("https://github.com/Marsek-prg/VoxLab/releases") { UseShellExecute=true }); break;
                case "soundSettings": Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute=true }); break;
                case "microphoneSettings": Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute=true }); break;
                case "openRecordings": OpenRecordings(); break;
                case "github": Process.Start(new ProcessStartInfo("https://github.com/marsek-prg/VoxLab") { UseShellExecute=true }); break;
                case "driverGuide": Process.Start(new ProcessStartInfo("https://github.com/marsek-prg/VoxLab/blob/main/docs/ROUTING.md") { UseShellExecute=true }); break;
                case "openCableArchive":
                    string archive=Path.Combine(AppContext.BaseDirectory,"Drivers","VB-CABLE","VBCABLE_Driver_Pack45.zip");
                    if(!File.Exists(archive)) throw new FileNotFoundException("Архив VB-CABLE отсутствует. Распакуйте весь portable-пакет VoxLab, включая папку App.");
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments="/select,\""+archive+"\"",UseShellExecute=true });
                    break;
            }
        }
        catch(Exception ex) { Notify(ex.Message,true); SendState(); }
    }

    private void RefreshDevices(bool initialize)
    {
        var inputs=AudioEngine.Devices(DataFlow.Capture); var outputs=AudioEngine.Devices(DataFlow.Render);
        if(initialize)
        {
            // Select a real microphone, prefer a virtual output. Never start automatically.
            var r=profile.Route;
            string? input=inputs.Any(x=>x.Id==r.InputId) ? r.InputId : inputs.FirstOrDefault(x=>x.IsDefault && !x.IsVirtual)?.Id ?? inputs.FirstOrDefault(x=>!x.IsVirtual)?.Id;
            string? output=outputs.Any(x=>x.Id==r.OutputId) ? r.OutputId : outputs.FirstOrDefault(x=>x.IsVirtual)?.Id;
            string? monitorId=outputs.Any(x=>x.Id==r.MonitorId) ? r.MonitorId : outputs.FirstOrDefault(x=>x.IsDefault && !x.IsVirtual)?.Id;
            profile=profile with { Route=r with { InputId=input,OutputId=output,MonitorId=monitorId } };
        }
        Send("devices",new { inputs,outputs });
    }
    private async Task StartEngine()
    {
        if(busy || engine.IsRunning) return;
        busy=true; Send("busy",true);
        try { faultHandled=false; var route=profile.Route; await Task.Run(()=>engine.Start(route)); SendState(); }
        catch(Exception ex) { Notify("Не удалось запустить звук: "+ex.Message,true); SendState(); }
        finally { busy=false; Send("busy",false); }
    }
    private void ApplyPreset(string id,bool reset=false)
    {
        profile=profile.SelectVoice(id,reset); ApplyAudioSettings(); SaveSoon(); SendState();
    }
    private void SavePreset(string? name)
    {
        if(string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Введите название пресета.");
        if(profile.CustomPresets.Count>=64) throw new InvalidOperationException("Можно сохранить до 64 своих пресетов.");
        name=name.Trim(); if(name.Length>48) name=name[..48];
        var preset=new VoicePreset("user-"+Guid.NewGuid().ToString("N"),name,"Мои","Ваши настройки голоса","Пользовательский пресет. Все параметры доступны в стойке эффектов.","sage","sliders",profile.Settings);
        profile.CustomPresets.Add(preset); profile=profile with { PresetId=preset.Id }; SaveSoon(); SendState(); Notify("Пресет «"+name+"» сохранён.");
    }
    private void ExportPreset()
    {
        var preset=PresetLibrary.All.Concat(profile.CustomPresets).FirstOrDefault(x=>x.Id==profile.PresetId) ?? PresetLibrary.All[0];
        var dialog=new Microsoft.Win32.SaveFileDialog { Filter="Пресет VoxLab (*.voxlab.json)|*.voxlab.json", FileName="VoxLab-preset.voxlab.json" };
        if(dialog.ShowDialog(this)==true) { File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(preset with { Settings=profile.Settings },ProfileStore.Json)); Notify("Пресет экспортирован."); }
    }
    private void ImportPreset()
    {
        var dialog=new Microsoft.Win32.OpenFileDialog { Filter="Пресет VoxLab (*.json)|*.json" };
        if(dialog.ShowDialog(this)!=true) return;
        if(new FileInfo(dialog.FileName).Length>100_000) throw new InvalidOperationException("Файл пресета слишком большой.");
        var preset=JsonSerializer.Deserialize<VoicePreset>(File.ReadAllText(dialog.FileName),ProfileStore.Json);
        if(preset?.Settings==null || string.IsNullOrWhiteSpace(preset.Name)) throw new InvalidOperationException("Это не пресет VoxLab.");
        if(profile.CustomPresets.Count>=64) throw new InvalidOperationException("Достигнут предел: 64 своих пресета.");
        preset=preset with { Id="user-"+Guid.NewGuid().ToString("N"),Name=preset.Name[..Math.Min(48,preset.Name.Length)],Category="Мои",Color="sage",Icon="sliders",Description="Импортированный пресет",Detail="Настройки импортированы из файла.",Settings=preset.Settings.Validate() };
        profile.CustomPresets.Add(preset); ApplyPreset(preset.Id); Notify("Пресет импортирован.");
    }
    private void ToggleRecording(bool quick=false)
    {
        if(engine.IsRecording) { var path=engine.StopRecording(); Notify("WAV сохранён: "+Path.GetFileName(path)); SendState(); return; }
        if(!engine.IsRunning) throw new InvalidOperationException("Сначала нажмите «Запустить голос».");
        string recordings=Path.Combine(AppPaths.Data,"Recordings");Directory.CreateDirectory(recordings);
        if(quick) { engine.StartRecording(Path.Combine(recordings,"VoxLab-"+DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff")+".wav")); SendState(); Notify("Запись началась. Повторное нажатие сохранит WAV в Data/Recordings."); return; }
        var dialog=new Microsoft.Win32.SaveFileDialog { Filter="WAV 32-bit float (*.wav)|*.wav",InitialDirectory=recordings,FileName="VoxLab-"+DateTime.Now.ToString("yyyy-MM-dd-HHmmss")+".wav" };
        if(dialog.ShowDialog(this)==true) { engine.StartRecording(dialog.FileName); SendState(); }
    }
    private void OpenRecordings()
    {
        string directory=engine.LastRecordingPath!=null ? Path.GetDirectoryName(engine.LastRecordingPath)! : Path.Combine(AppPaths.Data,"Recordings");
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo("explorer.exe") { Arguments="\""+directory+"\"",UseShellExecute=true });
    }
    private void SaveSoon() { saveTimer.Stop(); saveTimer.Start(); }
    private void CloseVoiceCheck()
    {
        checkClip=null;
        checkOperation?.Cancel();
        checkOperation=null;
    }
    private async Task RunVoiceCheck(string action)
    {
        if(checkOperation!=null || !IsVisible || closing) return;
        using var operation=new CancellationTokenSource();
        checkOperation=operation;
        var route=profile.Route;
        bool capture=action=="checkCapture";
        try {
            if(capture) {
                checkClip=null;
                if(engine.Muted) throw new InvalidOperationException("Включите микрофон, чтобы записать пробу.");
                var settings=profile.StudioProcessing ? profile.Settings : profile.Settings.WithoutStudioProcessing();
                Send("voiceCheck",new {phase="capture"});
                var clip=await Task.Run(()=>VoiceCheck.CaptureAsync(route,settings,operation.Token));
                if(checkOperation==operation) checkClip=clip;
            } else {
                var clip=checkClip ?? throw new InvalidOperationException("Сначала запишите короткую фразу.");
                bool processed=action=="checkProcessed";
                Send("voiceCheck",new {phase=processed?"processed":"original"});
                await Task.Run(()=>clip.PlayAsync(processed,route.MonitorId,route.MonitorVolume,operation.Token));
            }
            if(checkOperation==operation) Send("voiceCheck",new {phase="ready"});
        }
        catch(OperationCanceledException) when(operation.IsCancellationRequested) { }
        catch(Exception ex) {
            if(checkOperation==operation) Send("voiceCheck",new {phase="error",canPlay=checkClip!=null,
                message=ex is TimeoutException ? "Аудиоустройство не ответило. Проверьте подключение и повторите пробу." : ex.Message});
        }
        finally { if(checkOperation==operation) checkOperation=null; }
    }
    private void SendState()
    {
        if(engine.Muted && checkOperation!=null) { CloseVoiceCheck(); Send("voiceCheck",new {phase="closed"}); }
        if(tray!=null) tray.Text=engine.IsRecording ? "VoxLab · идёт запись WAV" : engine.IsRunning ? (engine.Muted ? "VoxLab · выход заглушён" : "VoxLab · голос включён") : "VoxLab · обработка остановлена";
        Send("state",new { profile=new {profile.PresetId,profile.Settings,profile.Route,profile.Favorites,profile.CustomPresets,profile.HotkeysEnabled,profile.HotkeyBindings,profile.StudioProcessing},presets=PresetLibrary.All, running=engine.IsRunning, bypass=engine.Bypass, muted=engine.Muted, recording=engine.IsRecording, hotkeyFailures, version=UpdateService.CurrentVersion });
        if(IsVisible && WindowState!=WindowState.Minimized) Send("meter",engine.MeterSnapshot());
        meterTimer.Interval=TimeSpan.FromMilliseconds(engine.IsRunning ? 60 : 1000);
    }
    private async Task UpdateVisualActivity()
    {
        if(!ready || closing) return;
        try
        {
            if(IsVisible && WindowState!=WindowState.Minimized) { Web.CoreWebView2.Resume(); SendState(); }
            else
            {
                CloseVoiceCheck(); Send("voiceCheck",new {phase="closed"});
                meterTimer.Interval=TimeSpan.FromMilliseconds(250);
                await Web.CoreWebView2.TrySuspendAsync();
                if(!closing && IsVisible && WindowState!=WindowState.Minimized) Web.CoreWebView2.Resume();
            }
        }
        catch(InvalidOperationException) { /* The window may be closing during suspension. */ }
        catch(COMException) { /* Audio continues even if the renderer cannot suspend. */ }
    }
    private void Notify(string text,bool error=false)=>Send("notice",new { text,error });
    private void Send(string type,object data)
    {
        if(ready && !closing) Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type,data },ProfileStore.Json));
    }
    private void ConfigureHotkeys()
    {
        foreach(int id in registeredKeys) UnregisterHotKey(handle,id);
        registeredKeys.Clear(); hotkeyFailures.Clear(); keyActions.Clear();
        if(!profile.HotkeysEnabled || capturingHotkey || handle==IntPtr.Zero) return;
        for(int i=0;i<profile.HotkeyBindings.Count;i++)
        {
            var binding=profile.HotkeyBindings[i]; int id=100+i;
            if(RegisterHotKey(handle,id,0x4000|(uint)binding.Modifiers,(uint)binding.Key)) { registeredKeys.Add(id); keyActions[id]=binding; }
            else hotkeyFailures.Add(binding.Label);
        }
    }
    private IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x0312) return IntPtr.Zero;
        handled=true;
        if(keyActions.TryGetValue(wParam.ToInt32(),out var binding)) ExecuteHotkey(binding);
        return IntPtr.Zero;
    }
    private void ExecuteHotkey(HotkeyBinding binding)
    {
        try
        {
            switch(binding.Action)
            {
                case "mute": engine.Muted=!engine.Muted; break;
                case "bypass": engine.Bypass=!engine.Bypass; break;
                case "engine": if(busy) return; if(engine.IsRunning) engine.Stop(); else _=StartEngine(); break;
                case "record": ToggleRecording(true); break;
                case "window": RestoreWindow(); break;
                case "preset": ApplyPreset(binding.PresetId!); break;
                case "next": case "previous":
                    var presets=PresetLibrary.All.Concat(profile.CustomPresets).ToList();
                    int current=presets.FindIndex(p=>p.Id==profile.PresetId);
                    ApplyPreset(presets[(current+presets.Count+(binding.Action=="next"?1:-1))%presets.Count].Id); break;
                case "strengthUp": case "strengthDown":
                    var settings=(profile.Settings with { EffectStrength=profile.Settings.EffectStrength+(binding.Action=="strengthUp"?10:-10) }).Validate();
                    profile=profile.Remember(profile.PresetId,settings); ApplyAudioSettings(); SaveSoon(); break;
            }
            SendState();
        }
        catch(Exception ex) { Notify(ex.Message,true); }
    }
    private void ShowUpdateResult()
    {
        string path=Path.Combine(AppPaths.Data,"Updates","last-result.json");
        try
        {
            if(!File.Exists(path)) return;
            using var result=JsonDocument.Parse(File.ReadAllText(path));
            bool success=result.RootElement.GetProperty("success").GetBoolean();
            Send("updateResult",new { success,message=success ? "Обновление установлено. Настройки и записи сохранены." : "Не удалось установить обновление. Резервная копия и подробности — в Data/Updates." });
        }
        catch { /* A damaged log must not prevent audio startup. */ }
    }
    // Explicit hardware check on an isolated profile. Use only the virtual input;
    // play silent buffers to exercise the physical endpoint without audible output.
    private async Task TestVoiceCheck(List<object> results)
    {
        var route=profile.Route;
        var cable=AudioEngine.Devices(DataFlow.Capture).First(d=>d.IsVirtual && d.Name.Contains("CABLE Output",StringComparison.OrdinalIgnoreCase));
        profile=profile with {Route=route with {InputId=cable.Id,MonitorVolume=25}};
        SendState();
        async Task Click(string id)=>await Web.CoreWebView2.ExecuteScriptAsync($"document.getElementById('{id}').click()");
        async Task<bool> WaitUntil(Func<bool> predicate,int limit=10000) {
            var watch=Stopwatch.StartNew();
            while(!predicate() && watch.ElapsedMilliseconds<limit) await Task.Delay(50);
            return predicate();
        }
        try {
            await Click("voiceCheckButton"); await WaitUntil(()=>checkOperation!=null,1000);
            await Click("checkClose");
            results.Add(new {test="cancel-local-capture",passed=await WaitUntil(()=>checkOperation==null,1500) && checkClip==null});
            await Click("voiceCheckButton");
            bool captured=await WaitUntil(()=>checkClip!=null);
            results.Add(new {test="five-seconds-without-live-output",passed=captured && checkClip!.Original.Length==checkClip.SampleRate*5 && !engine.IsRunning});
            if(checkClip!=null) {
                Array.Clear(checkClip.Original); Array.Clear(checkClip.Processed);
                foreach(string button in new[]{"checkOriginal","checkProcessed"}) {
                    await Click(button);
                    bool started=await WaitUntil(()=>checkOperation!=null,1000);
                    bool completed=await WaitUntil(()=>checkOperation==null);
                    string status=await Web.CoreWebView2.ExecuteScriptAsync("!document.querySelector('#checkOriginal').disabled && document.querySelector('#checkRoute').hidden");
                    results.Add(new {test="play-"+button,passed=started && completed && status=="true"});
                }
            }
        } finally {
            await Click("checkClose"); await Task.Delay(100);
            profile=profile with {Route=route}; SendState();
        }
    }
    private async Task RunSmoke()
    {
        await Task.Delay(1600);
        if(arguments.Contains("--ui-integration"))
        {
            var results=new List<object>();
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-preset=feminine]').click()");
            await Task.Delay(120);
            results.Add(new {test="four-essential-voice-controls",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('#quickParameters input').length===4 && !document.querySelector('#quick-robotMix')")== "true"});
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-preset=overload]').click()");
            await Task.Delay(120);
            results.Add(new {test="meme-overload-control",passed=profile.PresetId=="overload" && await Web.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('#quick-overload')")=="true"});
            results.Add(new {test="help-inside-routing",passed=await Web.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('#page-routing .routing-help') && !document.querySelector('nav [data-page=help]')")=="true"});
            if(arguments.Contains("--virtual-voice-check")) await TestVoiceCheck(results);
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-preset=robot]').focus();document.querySelector('[data-preset=robot]').click()");
            await Task.Delay(150);
            results.Add(new { test="preset-through-bridge",passed=profile.PresetId=="robot" && profile.Settings.RobotMix==82 });
            results.Add(new { test="preset-keyboard-focus",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.activeElement.dataset.preset==='robot'")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-favorite=robot]').focus();document.querySelector('[data-favorite=robot]').click()");
            await Task.Delay(100);
            results.Add(new { test="favorite-keyboard-focus",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.activeElement.dataset.favorite==='robot'")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-category=Персонажи]').focus();document.querySelector('[data-category=Персонажи]').click()");
            results.Add(new { test="category-keyboard-focus",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.activeElement.dataset.category==='Персонажи'")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page=rack]').click();document.querySelector('[data-toggle=gateEnabled]').focus();document.querySelector('[data-toggle=gateEnabled]').click()");
            await Task.Delay(100);
            results.Add(new { test="rack-switch-keyboard-focus",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.activeElement.dataset.toggle==='gateEnabled'")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page=voices]').click();document.querySelector('[data-category=Все]').click()");
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#quick-pitch').value='3.5';document.querySelector('#quick-pitch').dispatchEvent(new Event('input',{bubbles:true}))");
            await Task.Delay(150);
            results.Add(new { test="slider-through-bridge",passed=profile.Settings.Pitch==3.5 });
            results.Add(new { test="edited-description-hidden",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#selectedDescription').hidden")== "true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#quick-tone').value='-100';document.querySelector('#quick-tone').dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('[data-preset=feminine]').click();document.querySelector('[data-preset=robot]').click()");
            await Task.Delay(160);
            results.Add(new { test="rapid-switch-retains-sliders",passed=profile.PresetId=="robot" && profile.Settings.Pitch==3.5 && profile.Settings.Tone==-100 });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page=rack]').click();document.querySelector('#studioSwitch').click()");
            await Task.Delay(100);
            results.Add(new { test="studio-master-through-bridge",passed=!profile.StudioProcessing && await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-toggle=limiterEnabled]').disabled && document.querySelector('#rack-deEsser').disabled")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page=voices]').click();document.querySelector('[data-preset=feminine]').click();document.querySelector('[data-preset=robot]').click()");
            await Task.Delay(100);
            results.Add(new { test="global-studio-bypass-survives-preset",passed=!profile.StudioProcessing });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-action=saveDialog]').click();document.querySelector('#presetName').value='Integration test';document.querySelector('#saveForm button[value=save]').click()");
            await Task.Delay(150);
            results.Add(new { test="save-preset-through-dialog",passed=profile.CustomPresets.Any(p=>p.Name=="Integration test" && p.Settings.Pitch==3.5) });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-action=mute]').click()");
            await Task.Delay(100);
            results.Add(new { test="mute-through-bridge",passed=engine.Muted });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-action=bypass]').click()");
            await Task.Delay(100);
            results.Add(new { test="bypass-through-bridge",passed=engine.Bypass });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#quick-effectStrength').value='165';document.querySelector('#quick-effectStrength').dispatchEvent(new Event('input',{bubbles:true}))");
            await Task.Delay(120);
            results.Add(new { test="effect-strength-through-bridge",passed=profile.Settings.EffectStrength==165 });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page=preferences]').click();document.querySelector('#addHotkey').click();document.querySelector('#hotkey-action-11').value='strengthDown';document.querySelector('#hotkey-action-11').dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('#hotkey-key-11').click()");
            await Task.Delay(100);
            results.Add(new { test="capture-suspends-native-hotkeys",passed=capturingHotkey && registeredKeys.Count==0 });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#hotkey-key-11').dispatchEvent(new KeyboardEvent('keydown',{key:'F9',code:'F9',ctrlKey:true,shiftKey:true,bubbles:true}));document.querySelector('#saveHotkeys').click()");
            await Task.Delay(150);
            results.Add(new { test="custom-hotkey-capture-and-save",passed=profile.HotkeyBindings.Count==12 && profile.HotkeyBindings[^1]==new HotkeyBinding(120,6,"strengthDown") && !capturingHotkey });
            bool messageHandled=false;
            WindowMessage(handle,0x0312,new IntPtr(111),IntPtr.Zero,ref messageHandled);
            results.Add(new { test="custom-hotkey-dispatch",passed=registeredKeys.Contains(111) && messageHandled && profile.Settings.EffectStrength==155 });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#addHotkey').click();document.querySelector('#hotkey-key-12').click()");
            await Task.Delay(80);
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#hotkey-key-12').dispatchEvent(new KeyboardEvent('keydown',{key:'F9',code:'F9',ctrlKey:true,shiftKey:true,bubbles:true}))");
            results.Add(new { test="duplicate-hotkey-shows-inline-error",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#saveHotkeys').disabled && document.querySelector('#hotkeyError').textContent.includes('дважды')")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#hotkey-remove-12').click();document.querySelector('#saveHotkeys').click()");
            await Task.Delay(100);
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#hotkey-key-11').click()");
            await Task.Delay(80);
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#hotkey-key-11').dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',code:'Escape',bubbles:true}))");
            await Task.Delay(80);
            results.Add(new { test="escape-restores-native-hotkeys",passed=!capturingHotkey && registeredKeys.Contains(111) });
            results.Add(new { test="preferences-no-horizontal-overflow",passed=await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#workspace').scrollWidth <= document.querySelector('#workspace').clientWidth && document.querySelectorAll('.hotkey-row').length===12")=="true" });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page=voices]').click();document.querySelector('[data-preset=robot]').click()");
            await Task.Delay(120);
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#inspector [data-action=resetPreset]').click()");
            await Task.Delay(100);
            results.Add(new { test="reset-selected-voice-only",passed=profile.Settings==PresetLibrary.All.First(p=>p.Id=="robot").Settings && profile.CustomPresets.Any(p=>p.Settings.Pitch==3.5) });
            Close(); results.Add(new { test="close-hides-in-tray",passed=!IsVisible && !closing && tray?.Visible==true });
            var launch=new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden};
            launch.ArgumentList.Add("--data-dir");launch.ArgumentList.Add(AppPaths.Data);
            using(var second=Process.Start(launch)!) await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Delay(180);
            results.Add(new { test="second-launch-restores-hidden-window",passed=IsVisible && !closing });
            if(!IsVisible) RestoreWindow();
            File.WriteAllText(Path.Combine(AppPaths.Data,"integration.json"),JsonSerializer.Serialize(results,ProfileStore.Json));
            ApplyPreset("studio"); engine.Muted=false; engine.Bypass=false; SendState();
        }
        int pageArgument=Array.IndexOf(arguments,"--page");
        if(pageArgument>=0 && pageArgument+1<arguments.Length && new[]{"voices","rack","routing","record","preferences"}.Contains(arguments[pageArgument+1]))
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav-item[data-page="+arguments[pageArgument+1]+"]')?.click()");
        await Task.Delay(150);
        await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#toasts').replaceChildren();document.activeElement?.blur()");
        int index=Array.IndexOf(arguments,"--screenshot");
        if(index>=0 && index+1<arguments.Length)
        {
            using var stream=File.Create(Path.GetFullPath(arguments[index+1]));
            await Web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
        }
        var result=await Web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({title:document.title,cards:document.querySelectorAll('.preset-card').length,bridge:!!window.chrome.webview,overflow:document.documentElement.scrollWidth>innerWidth})");
        File.WriteAllText(Path.Combine(AppPaths.Data,"ui-smoke.json"),result);
        if(arguments.Contains("--exit-after-smoke")) ExitApplication();
    }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd,int id);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}
