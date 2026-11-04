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
    private bool ready, closing, busy, faultHandled;
    private IntPtr handle;

    public MainWindow(string[] args)
    {
        arguments=args; InitializeComponent();
        int sizeArgument=Array.IndexOf(args,"--size");
        if(sizeArgument>=0 && sizeArgument+1<args.Length)
        {
            var parts=args[sizeArgument+1].Split('x');
            if(parts.Length==2 && int.TryParse(parts[0],out int width) && int.TryParse(parts[1],out int height)) { Width=Math.Max(MinWidth,width); Height=Math.Max(MinHeight,height); }
        }
        Loaded+=async(_,_)=>await InitializeBrowser();
        IsVisibleChanged+=async(_,_)=>await UpdateVisualActivity();
        StateChanged+=async(_,_)=>await UpdateVisualActivity();
        SourceInitialized+=(_,_)=>
        {
            handle=new WindowInteropHelper(this).Handle;
            int dark=1; DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
            ConfigureHotkeys();
        };
        Closing+=(_,_)=>
        {
            closing=true; meterTimer.Stop(); saveTimer.Stop();
            foreach(int id in registeredKeys) UnregisterHotKey(handle,id);
            engine.Dispose(); tray?.Dispose();
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
        engine.Update(profile.Settings);
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
        menu.Items.Add("Остановить обработку",null,(_,_)=>Dispatcher.Invoke(()=> { engine.Stop(); SendState(); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выйти",null,(_,_)=>Dispatcher.Invoke(Close));
        tray.ContextMenuStrip=menu; tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(RestoreWindow);
    }
    private void RestoreWindow() { Show(); WindowState=WindowState.Normal; Activate(); }

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
                    ready=true; RefreshDevices(true); SendState(); meterTimer.Start();
                    if(arguments.Contains("--smoke")) await RunSmoke();
                    else if(arguments.Contains("--performance")) await RunPerformance();
                    break;
                case "settings":
                    var settings=data.Deserialize<VoiceSettings>(ProfileStore.Json)?.Validate() ?? new();
                    profile=profile with { Settings=settings }; engine.Update(settings); SaveSoon(); break;
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
                    if(profile.PresetId==id) ApplyPreset("studio"); SaveSoon(); SendState(); break;
                case "favorite":
                    var favorite=data.GetString()!; if(!profile.Favorites.Remove(favorite)) profile.Favorites.Add(favorite); SaveSoon(); SendState(); break;
                case "exportPreset": ExportPreset(); break;
                case "importPreset": ImportPreset(); break;
                case "hotkeys": profile=profile with { HotkeysEnabled=data.GetBoolean() }; ConfigureHotkeys(); SaveSoon(); SendState(); break;
                case "tray": Hide(); break;
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
    private void ApplyPreset(string id)
    {
        var preset=PresetLibrary.All.Concat(profile.CustomPresets).FirstOrDefault(x=>x.Id==id);
        if(preset==null) return;
        // Keep input/output gain consistent when auditioning characters.
        var settings=preset.Settings with { InputGain=profile.Settings.InputGain, OutputGain=profile.Settings.OutputGain };
        profile=profile with { PresetId=id,Settings=settings }; engine.Update(settings); SaveSoon(); SendState();
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
    private void ToggleRecording()
    {
        if(engine.IsRecording) { var path=engine.StopRecording(); Notify("WAV сохранён: "+Path.GetFileName(path)); SendState(); return; }
        if(!engine.IsRunning) throw new InvalidOperationException("Сначала нажмите «Запустить голос».");
        string recordings=Path.Combine(AppPaths.Data,"Recordings");Directory.CreateDirectory(recordings);
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
    private void SendState()
    {
        Send("state",new { profile,presets=PresetLibrary.All, running=engine.IsRunning, bypass=engine.Bypass, muted=engine.Muted, recording=engine.IsRecording, hotkeyFailures, version="0.1.1" });
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
        registeredKeys.Clear(); hotkeyFailures.Clear();
        if(!profile.HotkeysEnabled) return;
        foreach(var (id,key,label) in new[] { (1,0x4d,"Ctrl+Alt+M"),(2,0x42,"Ctrl+Alt+B"),(3,0x53,"Ctrl+Alt+S") }.Concat(Enumerable.Range(0,8).Select(i=>(10+i,0x31+i,"Ctrl+Alt+"+(i+1)))))
        {
            if(RegisterHotKey(handle,id,0x4003,(uint)key)) registeredKeys.Add(id); else hotkeyFailures.Add(label);
        }
    }
    private IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x0312) return IntPtr.Zero;
        int id=wParam.ToInt32(); handled=true;
        if(id==1) engine.Muted=!engine.Muted;
        else if(id==2) engine.Bypass=!engine.Bypass;
        else if(id==3) { if(engine.IsRunning) engine.Stop(); else _=StartEngine(); }
        else if(id>=10 && id<18) ApplyPreset(PresetLibrary.All[id-10].Id);
        SendState(); return IntPtr.Zero;
    }
    private async Task RunSmoke()
    {
        await Task.Delay(1600);
        if(arguments.Contains("--ui-integration"))
        {
            var results=new List<object>();
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
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-action=saveDialog]').click();document.querySelector('#presetName').value='Integration test';document.querySelector('#saveForm button[value=save]').click()");
            await Task.Delay(150);
            results.Add(new { test="save-preset-through-dialog",passed=profile.CustomPresets.Any(p=>p.Name=="Integration test" && p.Settings.Pitch==3.5) });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-action=mute]').click()");
            await Task.Delay(100);
            results.Add(new { test="mute-through-bridge",passed=engine.Muted });
            await Web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-action=bypass]').click()");
            await Task.Delay(100);
            results.Add(new { test="bypass-through-bridge",passed=engine.Bypass });
            File.WriteAllText(Path.Combine(AppPaths.Data,"integration.json"),JsonSerializer.Serialize(results,ProfileStore.Json));
            ApplyPreset("studio"); engine.Muted=false; engine.Bypass=false; SendState();
        }
        int pageArgument=Array.IndexOf(arguments,"--page");
        if(pageArgument>=0 && pageArgument+1<arguments.Length && new[]{"voices","rack","routing","record","help"}.Contains(arguments[pageArgument+1]))
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
        if(arguments.Contains("--exit-after-smoke")) Close();
    }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd,int id);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}
