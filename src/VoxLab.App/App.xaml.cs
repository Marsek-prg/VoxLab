using System.IO;
using System.Windows;
using Application=System.Windows.Application;

namespace VoxLab.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
}

internal static class AppPaths
{
    public static string Data { get; }=Resolve();
    public static string Profile=>Path.Combine(Data,"profile.json");
    private static string Resolve()
    {
        var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--data-dir");
        string path=index>=0 && index+1<args.Length ? Path.GetFullPath(args[index+1]) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"VoxLab");
        Directory.CreateDirectory(path); return path;
    }
}
