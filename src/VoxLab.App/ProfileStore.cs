using System.IO;
using System.Text.Json;
using VoxLab.App.Audio;
using VoxLab.Core;

namespace VoxLab.App;
public sealed record UserProfile
{
    public VoiceSettings Settings { get; init; }=new();
    public RouteSettings Route { get; init; }=new();
    public string PresetId { get; init; }="studio";
    public List<VoicePreset> CustomPresets { get; init; }=[];
    public List<string> Favorites { get; init; }=["studio","deep","robot","feminine"];
    public bool HotkeysEnabled { get; init; }=true;
    public List<HotkeyBinding> HotkeyBindings { get; init; }=Hotkeys.Defaults();
}
internal static class ProfileStore
{
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web) { WriteIndented=true };
    public static UserProfile Load()
    {
        try
        {
            if(!File.Exists(AppPaths.Profile) || new FileInfo(AppPaths.Profile).Length>2_000_000) return new();
            var p=JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(AppPaths.Profile),Json) ?? new();
            try { Hotkeys.Validate(p.HotkeyBindings, PresetLibrary.All.Concat(p.CustomPresets ?? []).Where(x=>x!=null).Select(x=>x.Id)); }
            catch { p=p with { HotkeyBindings=Hotkeys.Defaults() }; }
            return p with { Settings=(p.Settings ?? new()).Validate(), Route=p.Route ?? new(),
                Favorites=(p.Favorites ?? []).Take(128).ToList(),
                CustomPresets=(p.CustomPresets ?? []).Where(x=>x!=null && x.Settings!=null && !string.IsNullOrWhiteSpace(x.Name))
                .Take(64).Select(x=>x with { Settings=x.Settings.Validate(), Category="Мои", Icon="sliders", Color="sage" }).ToList() };
        }
        catch { return new(); }
    }
    public static void Save(UserProfile profile)
    {
        string temporary=AppPaths.Profile+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(profile,Json));
        File.Move(temporary,AppPaths.Profile,true);
    }
}
