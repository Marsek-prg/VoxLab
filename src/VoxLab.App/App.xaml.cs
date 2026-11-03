using System.IO;
using System.Windows;
using Application=System.Windows.Application;

namespace VoxLab.App;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            string directory=AppPaths.Data;
            string probe=Path.Combine(directory,".write-test-"+Guid.NewGuid().ToString("N"));
            using(new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose)) { }
            string key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())));
            instance=new Mutex(true,"Local\\VoxLab-"+key,out bool first);
            if(!first) { instance.Dispose(); instance=null; System.Windows.MessageBox.Show("VoxLab из этой папки уже запущен. Откройте его через значок в системном трее.","VoxLab"); Shutdown(); return; }
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
    }
    protected override void OnExit(ExitEventArgs e) { instance?.ReleaseMutex(); instance?.Dispose(); base.OnExit(e); }
}

internal static class AppPaths
{
    public static string Data { get; }=Resolve();
    public static string Profile=>Path.Combine(Data,"profile.json");
    private static string Resolve()
    {
        var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--data-dir");
        string path=index>=0 && index+1<args.Length ? Path.GetFullPath(args[index+1]) : Path.Combine(AppContext.BaseDirectory,"Data");
        Directory.CreateDirectory(path); return path;
    }
}
