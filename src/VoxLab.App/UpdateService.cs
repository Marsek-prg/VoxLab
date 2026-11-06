using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VoxLab.App;

public sealed record UpdateFile(string Path, string Sha256, long Length, string Component);
public sealed record UpdateBundle(string Id, string Asset, string Sha256, long Size);
public sealed record UpdateManifest(int Format, string Version, List<UpdateFile> Files, List<UpdateBundle> Bundles);
public sealed record UpdateStatus(string Phase, string Message, string? Version=null, long DownloadBytes=0, int ChangedFiles=0);

public sealed class UpdateService : IDisposable
{
    public const string CurrentVersion="0.2.2";
    private const string Releases="https://api.github.com/repos/Marsek-prg/VoxLab/releases/latest";
    private readonly HttpClient client;
    private readonly Version installedVersion;
    private readonly CancellationTokenSource cancellation=new();
    private UpdateManifest? manifest;
    private List<UpdateFile> changed=[];
    private Dictionary<string,string> assets=[];
    private string? stage;
    private bool busy;
    public UpdateStatus Status { get; private set; }=new("idle","Проверка по запросу. Настройки, пресеты и записи сохраняются.");
    public event Action<UpdateStatus>? Changed;

    public UpdateService(HttpMessageHandler? transport=null,Version? installedVersion=null) {
        this.installedVersion=installedVersion ?? Version.Parse(CurrentVersion);
        client=transport is null ? new HttpClient() : new HttpClient(transport);
        client.Timeout=TimeSpan.FromMinutes(15); client.DefaultRequestHeaders.UserAgent.ParseAdd("VoxLab/"+CurrentVersion);
    }
    private void Report(UpdateStatus status) { Status=status; Changed?.Invoke(status); }

    public async Task CheckAsync(string root)
    {
        if(busy) return; busy=true; manifest=null; changed=[]; stage=null;
        Report(new("checking","Проверяем выпуск на GitHub…"));
        try
        {
            using var release=JsonDocument.Parse(await GetSmall(Releases,2_000_000));
            var json=release.RootElement;
            string tag=json.GetProperty("tag_name").GetString() ?? "";
            if(!Version.TryParse(tag.TrimStart('v'),out var version)) throw new InvalidDataException("Неизвестный номер выпуска.");
            if(version<=installedVersion) { Report(new("current","У вас актуальная версия "+CurrentVersion+".")); return; }
            if(json.GetProperty("prerelease").GetBoolean() || json.GetProperty("draft").GetBoolean()) throw new InvalidDataException("Это тестовый выпуск.");
            assets=json.GetProperty("assets").EnumerateArray().ToDictionary(x=>x.GetProperty("name").GetString()!,x=>x.GetProperty("browser_download_url").GetString()!);
            if(!assets.TryGetValue("VoxLab-update.json",out var manifestUrl)) throw new InvalidDataException("В выпуске нет пакета для встроенного обновления. Откройте релизы на GitHub.");
            ValidateAssetUrl(manifestUrl,tag);
            manifest=JsonSerializer.Deserialize<UpdateManifest>(await GetSmall(manifestUrl,2_000_000),ProfileStore.Json) ?? throw new InvalidDataException("Пустой манифест.");
            ValidateManifest(manifest,version.ToString());
            foreach(var bundle in manifest.Bundles)
            {
                if(!assets.TryGetValue(bundle.Asset,out var url)) throw new InvalidDataException("В выпуске отсутствует компонент "+bundle.Id);
                ValidateAssetUrl(url,tag);
            }
            changed=await Task.Run(()=>FindChanges(root,manifest),cancellation.Token);
            long size=manifest.Bundles.Where(x=>changed.Any(f=>f.Component==x.Id)).Sum(x=>x.Size);
            Report(new("available","Доступна версия "+manifest.Version+". Загрузим только изменившиеся компоненты.",manifest.Version,size,changed.Count));
        }
        catch(Exception ex) when(!cancellation.IsCancellationRequested) { Report(new("error","Не удалось проверить обновление: "+ex.Message)); }
        finally { busy=false; }
    }

    public async Task PrepareAsync(string root,string data)
    {
        if(busy || manifest is null || Status.Phase!="available") return; busy=true;
        var previous=Status;
        Report(previous with { Phase="downloading",Message="Подготавливаем обновление…" });
        try
        {
            stage=Path.Combine(data,"Updates",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string payload=Path.Combine(stage,"payload"); Directory.CreateDirectory(payload);
            // Recheck local changes at download time. Never touch Data or unlisted files.
            changed=await Task.Run(()=>FindChanges(root,manifest),cancellation.Token);
            var needed=manifest.Bundles.Where(x=>changed.Any(f=>f.Component==x.Id)).ToArray();
            for(int i=0;i<needed.Length;i++)
            {
                var bundle=needed[i];
                Report(previous with { Phase="downloading",Message=$"Загрузка компонента {i+1} из {needed.Length}…" });
                string zip=Path.Combine(stage,bundle.Id+".zip");
                await Download(assets[bundle.Asset],zip,bundle.Size);
                await Task.Run(()=> {
                    if(Hash(zip)!=bundle.Sha256) throw new InvalidDataException("Контрольная сумма архива не совпала.");
                    ExtractBundle(zip,payload,manifest,bundle.Id,changed);
                },cancellation.Token);
                File.Delete(zip);
            }
            string helper=Path.Combine(AppContext.BaseDirectory,"Update-VoxLab.ps1");
            File.Copy(helper,Path.Combine(stage,"Update-VoxLab.ps1"));
            var process=Process.GetCurrentProcess();
            File.WriteAllText(Path.Combine(stage,"plan.json"),JsonSerializer.Serialize(new {
                root=Path.GetFullPath(root),data=Path.GetFullPath(data),payload,version=manifest.Version,
                parentPid=Environment.ProcessId,parentStarted=process.StartTime.ToUniversalTime().Ticks,
                files=changed
            },ProfileStore.Json));
            Report(previous with { Phase="ready",Message="Обновление проверено и готово. Завершите запись перед перезапуском." });
        }
        catch(Exception ex) when(!cancellation.IsCancellationRequested) { stage=null; Report(previous with { Phase="error",Message="Обновление не установлено: "+ex.Message }); }
        finally { busy=false; }
    }

    public async Task LaunchInstallerAsync()
    {
        if(busy || stage is null || Status.Phase!="ready") throw new InvalidOperationException("Сначала загрузите обновление.");
        busy=true;
        var previous=Status; Report(Status with { Phase="installing",Message="Готовим безопасный перезапуск…" });
        try
        {
            string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
            var info=new ProcessStartInfo(powershell) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=stage };
            foreach(string arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Path.Combine(stage,"Update-VoxLab.ps1"),"-PlanFile",Path.Combine(stage,"plan.json")}) info.ArgumentList.Add(arg);
            using var installer=Process.Start(info) ?? throw new IOException("Не удалось запустить обновление.");
            string ready=Path.Combine(stage,"installer-ready");
            for(int i=0;i<100;i++)
            {
                if(File.Exists(ready)) return;
                if(installer.HasExited) throw new IOException("Установщик завершился до начала работы. Проверьте Data/Updates/last-result.json.");
                await Task.Delay(100);
            }
            // The helper also times out without modifying files if this process stays alive.
            throw new IOException("Установщик не ответил. VoxLab продолжает работать; повторите проверку позже.");
        }
        catch { Report(previous); throw; }
        finally { busy=false; }
    }

    private async Task<string> GetSmall(string url,int limit)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        response.EnsureSuccessStatusCode();
        using var input=await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output=new MemoryStream(); byte[] buffer=new byte[16384]; int n;
        while((n=await input.ReadAsync(buffer,timeout.Token))>0) {
            if(output.Length+n>limit) throw new InvalidDataException("Ответ сервера слишком большой.");
            output.Write(buffer,0,n);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
    private async Task Download(string url,string path,long expected)
    {
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancellation.Token);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength is long length && length!=expected) throw new InvalidDataException("Размер загрузки отличается от манифеста.");
        using var input=await response.Content.ReadAsStreamAsync(cancellation.Token);
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
        byte[] buffer=new byte[65536]; long total=0; int n; var progress=Stopwatch.StartNew();
        while((n=await input.ReadAsync(buffer,cancellation.Token))>0) {
            total+=n; if(total>expected) throw new InvalidDataException("Архив превышает ожидаемый размер.");
            await output.WriteAsync(buffer.AsMemory(0,n),cancellation.Token);
            if(progress.ElapsedMilliseconds>=400) { Report(Status with { Message=$"Загрузка: {total/1048576.0:F1} из {expected/1048576.0:F1} МБ…" }); progress.Restart(); }
        }
        if(total!=expected) throw new InvalidDataException("Загрузка прервана. Можно повторить проверку и скачать заново.");
    }
    private static void ValidateAssetUrl(string url,string tag)
    {
        string prefix="https://github.com/Marsek-prg/VoxLab/releases/download/"+tag+"/";
        if(!url.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Query!="" || uri.Fragment!="")
            throw new InvalidDataException("Файл обновления должен принадлежать выпуску VoxLab на GitHub.");
    }
    public static void ValidateManifest(UpdateManifest value,string version)
    {
        if(value.Format!=1 || value.Version!=version || value.Files is null || value.Bundles is null || value.Files.Count is <1 or >2000 || value.Bundles.Count is <1 or >10)
            throw new InvalidDataException("Неподдерживаемый манифест обновления.");
        var ids=new HashSet<string>(); var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var b in value.Bundles) {
            if(b is null || !Regex.IsMatch(b.Id ?? "","^[a-z]{1,20}$") || !Regex.IsMatch(b.Asset ?? "","^VoxLab-[A-Za-z0-9.-]+\\.zip$") || !ids.Add(b.Id!) || !names.Add(b.Asset!) || !ValidHash(b.Sha256) || b.Size is <1 or >350_000_000)
                throw new InvalidDataException("Некорректный компонент обновления.");
        }
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var f in value.Files) {
            if(f is null || !SafeRelativePath(f.Path) || !paths.Add(f.Path) || !ids.Contains(f.Component) || !ValidHash(f.Sha256) || f.Length is <0 or >350_000_000)
                throw new InvalidDataException("Некорректный файл обновления.");
        }
        if(value.Files.Sum(f=>f.Length)>800_000_000 || !paths.Contains("VoxLab.exe") || !paths.Contains("App/VoxLab.dll")) throw new InvalidDataException("Неполный или слишком большой выпуск.");
        foreach(string path in paths) {
            int slash=path.IndexOf('/');
            while(slash>=0) { if(paths.Contains(path[..slash])) throw new InvalidDataException("Конфликт файла и папки."); slash=path.IndexOf('/',slash+1); }
        }
    }
    public static bool SafeRelativePath(string? path)
    {
        if(string.IsNullOrWhiteSpace(path) || path.Length>220 || path.Contains('\\') || path.Contains(':') || path.Any(c=>c<32) || path.IndexOfAny(['*','?','"','<','>','|'])>=0) return false;
        if(path is not "VoxLab.exe" and not "Начни здесь.txt" && !path.StartsWith("App/",StringComparison.Ordinal)) return false;
        return path.Split('/').All(p=>p.Length>0 && p is not "." and not ".." && !p.EndsWith(' ') && !p.EndsWith('.') &&
            !p.Equals(".update.lock",StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(p,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\\.|$)",RegexOptions.IgnoreCase));
    }
    private static bool ValidHash(string? hash)=>hash!=null && Regex.IsMatch(hash,"^[0-9a-f]{64}$");
    public static string SafeDestination(string root,string relative)
    {
        if(!SafeRelativePath(relative)) throw new InvalidDataException("Небезопасный путь обновления.");
        root=Path.GetFullPath(root); string full=Path.GetFullPath(Path.Combine(root,relative));
        if(!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Путь за пределами VoxLab.");
        for(string? p=full;p!=null;p=Path.GetDirectoryName(p)) {
            if((File.Exists(p)||Directory.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new IOException("Обновление через символические ссылки не поддерживается.");
        }
        return full;
    }
    public static string Hash(string path) { using var file=File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
    public static List<UpdateFile> FindChanges(string root,UpdateManifest value)=>value.Files.Where(f=> {
        string target=SafeDestination(root,f.Path);
        return !File.Exists(target) || new FileInfo(target).Length!=f.Length || Hash(target)!=f.Sha256;
    }).ToList();
    public static void ExtractBundle(string zip,string payload,UpdateManifest value,string component,IReadOnlyList<UpdateFile> wanted)
    {
        using var archive=ZipFile.OpenRead(zip);
        var declared=value.Files.Where(f=>f.Component==component).ToDictionary(f=>f.Path,StringComparer.OrdinalIgnoreCase);
        var needed=wanted.Where(f=>f.Component==component).Select(f=>f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in archive.Entries) {
            if(!seen.Add(entry.FullName) || !declared.TryGetValue(entry.FullName,out var f) || entry.Length!=f.Length) throw new InvalidDataException("Содержимое архива не соответствует манифесту.");
            if(!needed.Contains(f.Path)) continue;
            string target=SafeDestination(payload,f.Path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using(var input=entry.Open()) using(var output=File.Create(target)) {
                byte[] buffer=new byte[65536]; long total=0; int n;
                while((n=input.Read(buffer))>0) { total+=n; if(total>f.Length) throw new InvalidDataException("Размер файла превышен."); output.Write(buffer,0,n); }
                if(total!=f.Length) throw new InvalidDataException("Неполный файл.");
            }
            if(Hash(target)!=f.Sha256) throw new InvalidDataException("Повреждён файл "+f.Path);
        }
        if(!needed.IsSubsetOf(seen)) throw new InvalidDataException("В архиве отсутствуют нужные файлы.");
    }
    public void Dispose() { cancellation.Cancel(); client.Dispose(); cancellation.Dispose(); }
}
