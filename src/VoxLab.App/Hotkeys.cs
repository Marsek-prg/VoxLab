namespace VoxLab.App;

public sealed record HotkeyBinding(int Key, int Modifiers, string Action, string? PresetId = null)
{
    public string Label => ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") +
        ((Modifiers & 4) != 0 ? "Shift + " : "") + (Key is >= 112 and <= 135 ? "F" + (Key - 111) :
        Key == 32 ? "Space" : Key is >= 37 and <= 40 ? new[] { "←", "↑", "→", "↓" }[Key - 37] : ((char)Key).ToString());
}

public static class Hotkeys
{
    public static readonly string[] Actions = ["mute", "bypass", "engine", "record", "window", "previous", "next", "strengthDown", "strengthUp", "preset"];
    public static List<HotkeyBinding> Defaults() => new List<HotkeyBinding> {
        new(77,3,"mute"), new(66,3,"bypass"), new(83,3,"engine")
    }.Concat(VoxLab.Core.PresetLibrary.All.Take(8).Select((p,i)=>new HotkeyBinding(49+i,3,"preset",p.Id))).ToList();

    public static void Validate(IReadOnlyList<HotkeyBinding> bindings, IEnumerable<string> presetIds)
    {
        if(bindings.Count>64) throw new ArgumentException("Можно назначить до 64 сочетаний.");
        var combinations=new HashSet<(int,int)>(); var presets=presetIds.ToHashSet();
        foreach(var binding in bindings)
        {
            if(binding is null) throw new ArgumentException("Пустое сочетание клавиш.");
            int key=binding.Key, modifiers=binding.Modifiers;
            bool function=key is >=112 and <=135;
            if(!(key is >=65 and <=90 or >=48 and <=57 or >=37 and <=40 or 32 || function) || key==123 ||
                modifiers<0 || modifiers>7 || (!function && (modifiers&3)==0) || (key==115 && (modifiers&1)!=0))
                throw new ArgumentException("Используйте Ctrl/Alt + букву, цифру, стрелку или Space; либо F1–F24. F12 и Alt+F4 зарезервированы Windows.");
            if(!Actions.Contains(binding.Action) || (binding.Action=="preset" && !presets.Contains(binding.PresetId ?? "")))
                throw new ArgumentException("Выберите действие и существующий пресет.");
            if(!combinations.Add((key,modifiers))) throw new ArgumentException("Сочетание «"+binding.Label+"» назначено дважды.");
        }
    }
}
