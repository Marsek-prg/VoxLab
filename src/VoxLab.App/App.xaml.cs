using System.IO;
using System.Windows;
using Application=System.Windows.Application;

namespace VoxLab.App;
public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? wake;
    private RegisteredWaitHandle? wakeRegistration;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            string directory=AppPaths.Data;
            string updateLock=Path.Combine(AppPaths.Root,"App",".update.lock");
            if(File.Exists(updateLock))
            {
                try { using var unlocked=File.Open(updateLock,FileMode.Open,FileAccess.Read,FileShare.ReadWrite); }
                catch(IOException) { System.Windows.MessageBox.Show("VoxLab обновляется. Дождитесь автоматического перезапуска.","VoxLab"); Shutdown(); return; }
            }
            string probe=Path.Combine(directory,".write-test-"+Guid.NewGuid().ToString("N"));
            using(new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose)) { }
            string key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())));
            instance=new Mutex(true,"Local\\VoxLab-"+key,out bool first);
            wake=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\VoxLab-Show-"+key);
            if(!first) { instance.Dispose(); instance=null; wake.Set(); Shutdown(); return; }
        }
        catch(Exception ex)
        {
            System.Windows.MessageBox.Show("Не удалось открыть папку Data рядом с VoxLab.exe. Распакуйте всю portable-папку в место, доступное для записи, например Документы.\n\n"+ex.GetBaseException().Message,"VoxLab",MessageBoxButton.OK,MessageBoxImage.Error);
            Shutdown(1); return;
        }
        DispatcherUnhandledException+=(sender,args)=>
        {
            try { File.AppendAllText(Path.Combine(AppPaths.Data,"errors.log"),DateTime.Now+" "+args.Exception+Environment.NewLine); } catch { }
            System.Windows.MessageBox.Show("Не удалось выполнить действие. Перезапустите VoxLab.\n"+args.Exception.Message,"VoxLab",MessageBoxButton.OK,MessageBoxImage.Error);
            args.Handled=true;
        };
        if(e.Args.Contains("--diagnostics") || e.Args.Contains("--audio-smoke"))
        {
            _=Diagnostics.Run(e.Args).ContinueWith(_=>Dispatcher.Invoke(Shutdown));
            return;
        }
        var window=new MainWindow(e.Args); MainWindow=window; window.Show();
        wakeRegistration=ThreadPool.RegisterWaitForSingleObject(wake!,(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{
            if(!window.ExitRequested) window.RestoreWindow();
        })),null,Timeout.Infinite,false);
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { if(MainWindow is MainWindow w) w.ExitRequested=true; base.OnSessionEnding(e); }
    protected override void OnExit(ExitEventArgs e) { wakeRegistration?.Unregister(null); wake?.Dispose(); instance?.ReleaseMutex(); instance?.Dispose(); base.OnExit(e); }
}

internal static class AppPaths
{
    // Packaged apphost lives one level above the managed assembly. In normal
    // development builds both are together; dotnet-hosted runs use the DLL folder.
    public static string Root { get; }=Environment.ProcessPath is string executable &&
        !Path.GetFileName(executable).Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(executable)! : AppContext.BaseDirectory;
    public static string Data { get; }=Resolve();
    public static string Profile=>Path.Combine(Data,"profile.json");
    private static string Resolve()
    {
        var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--data-dir");
        string path=index>=0 && index+1<args.Length ? Path.GetFullPath(args[index+1]) : Path.Combine(Root,"Data");
        Directory.CreateDirectory(path); return path;
    }
}
