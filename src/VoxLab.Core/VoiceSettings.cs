namespace VoxLab.Core;

/// <summary>Immutable, validated snapshots are exchanged at audio-block boundaries.</summary>
public sealed record VoiceSettings
{
    public double EffectStrength { get; init; } = 100;
    public double Pitch { get; init; }
    public double Tone { get; init; }
    public double InputGain { get; init; }
    public double OutputGain { get; init; } = -3;
    public double HighpassHz { get; init; } = 75;
    public bool HighpassEnabled { get; init; } = true;
    public bool EqualizerEnabled { get; init; } = true;
    public bool DeEsserEnabled { get; init; } = true;
    public bool LimiterEnabled { get; init; } = true;
    public double Bass { get; init; }
    public double Mid { get; init; }
    public double Treble { get; init; }
    public bool GateEnabled { get; init; } = true;
    public double GateThreshold { get; init; } = -48;
    public double GateAttackMs { get; init; } = 4;
    public double GateHoldMs { get; init; } = 90;
    public double GateReleaseMs { get; init; } = 140;
    public bool CompressorEnabled { get; init; } = true;
    public double CompressorThreshold { get; init; } = -20;
    public double CompressorRatio { get; init; } = 3;
    public double CompressorAttackMs { get; init; } = 12;
    public double CompressorReleaseMs { get; init; } = 140;
    public double CompressorMakeup { get; init; } = 2;
    public double DeEsser { get; init; } = 20;
    public double RobotMix { get; init; }
    public double RobotFrequency { get; init; } = 70;
    public double EchoMix { get; init; }
    public double EchoDelayMs { get; init; } = 260;
    public double EchoFeedback { get; init; } = 30;
    public double ReverbMix { get; init; }
    public double ChorusMix { get; init; }
    public double Drive { get; init; }
    public bool Radio { get; init; }
    public double RadioAmount { get; init; } = 100;
    public double CrushMix { get; init; }
    public double CrushBits { get; init; } = 6;
    public double CrushRate { get; init; } = 6000;
    public double TremoloMix { get; init; }
    public double TremoloRate { get; init; } = 8;
    public double FlangerMix { get; init; }
    public double FlangerRate { get; init; } = .35;

    public VoiceSettings WithoutStudioProcessing() => this with {
        HighpassEnabled=false, GateEnabled=false, EqualizerEnabled=false,
        DeEsserEnabled=false, CompressorEnabled=false, LimiterEnabled=false
    };

    public VoiceSettings Validate() => this with
    {
        EffectStrength=Limit(EffectStrength,0,200), RadioAmount=Limit(RadioAmount,0,100),
        CrushMix=Limit(CrushMix,0,100), CrushBits=Limit(CrushBits,3,16), CrushRate=Limit(CrushRate,1500,24000),
        TremoloMix=Limit(TremoloMix,0,100), TremoloRate=Limit(TremoloRate,.5,30),
        FlangerMix=Limit(FlangerMix,0,100), FlangerRate=Limit(FlangerRate,.05,3),
        Pitch = Limit(Pitch, -12, 12), Tone = Limit(Tone, -100, 100),
        InputGain = Limit(InputGain, -24, 24), OutputGain = Limit(OutputGain, -36, 12),
        HighpassHz = Limit(HighpassHz, 20, 300), Bass = Limit(Bass, -12, 12),
        Mid = Limit(Mid, -12, 12), Treble = Limit(Treble, -12, 12),
        GateThreshold = Limit(GateThreshold, -72, -12), GateAttackMs = Limit(GateAttackMs, 1, 40),
        GateHoldMs = Limit(GateHoldMs, 10, 500), GateReleaseMs = Limit(GateReleaseMs, 20, 600),
        CompressorThreshold = Limit(CompressorThreshold, -48, -3), CompressorRatio = Limit(CompressorRatio, 1, 12),
        CompressorAttackMs = Limit(CompressorAttackMs, 1, 80), CompressorReleaseMs = Limit(CompressorReleaseMs, 30, 700),
        CompressorMakeup = Limit(CompressorMakeup, 0, 18), DeEsser = Limit(DeEsser, 0, 100),
        RobotMix = Limit(RobotMix, 0, 100), RobotFrequency = Limit(RobotFrequency, 20, 180),
        EchoMix = Limit(EchoMix, 0, 70), EchoDelayMs = Limit(EchoDelayMs, 60, 900),
        EchoFeedback = Limit(EchoFeedback, 0, 70), ReverbMix = Limit(ReverbMix, 0, 70),
        ChorusMix = Limit(ChorusMix, 0, 100), Drive = Limit(Drive, 0, 100)
    };

    // Character intensity, not a dry/wet crossfade: pitch has variable latency.
    // Voice cleanup and user gain stay independent of the character amount.
    public VoiceSettings Effective()
    {
        var s=Validate(); double a=s.EffectStrength/100;
        return (s with { EffectStrength=100,Pitch=s.Pitch*a,Tone=s.Tone*a,Bass=s.Bass*a,Mid=s.Mid*a,Treble=s.Treble*a,
            RobotMix=s.RobotMix*a,Drive=s.Drive*a,EchoMix=s.EchoMix*a,ReverbMix=s.ReverbMix*a,ChorusMix=s.ChorusMix*a,
            RadioAmount=s.RadioAmount*a,CrushMix=s.CrushMix*a,TremoloMix=s.TremoloMix*a,FlangerMix=s.FlangerMix*a }).Validate();
    }

    private static double Limit(double value, double min, double max) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : Math.Clamp(0, min, max);
}

public sealed record VoicePreset(string Id, string Name, string Category, string Description,
    string Detail, string Color, string Icon, VoiceSettings Settings);

public static class PresetLibrary
{
    public static IReadOnlyList<VoicePreset> All { get; } =
    [
        new("studio", "Чистый студийный", "Студия", "Ваш голос, чуть собраннее", "Мягкая компрессия, срез гула и контроль сибилянтов. Хорошая точка отсчёта для разговора.", "sage", "mic", new()),
        new("broadcast", "Ведущий эфира", "Студия", "Плотный и близкий", "Тёплая подача с лёгким понижением тона и компрессией для стрима или подкаста.", "sand", "broadcast", new() { Pitch=-1, Bass=3, Mid=1.5, Treble=1, CompressorRatio=4, CompressorThreshold=-24, CompressorMakeup=4, DeEsser=40 }),
        new("feminine", "Женский тембр", "Голоса", "Выше, мягче, светлее", "Высота +4 полутона и светлая окраска. Характерный DSP-эффект: естественность зависит от исходного голоса.", "rose", "feather", new() { Pitch=4, Tone=30, Bass=-2, Treble=1.5, DeEsser=35, CompressorRatio=2.5 }),
        new("deep", "Глубокий бас", "Голоса", "Ниже на пять полутонов", "Низкий регистр с тёплой окраской. Оставляет разборчивость речи без чрезмерного басового гула.", "sky", "layers", new() { Pitch=-5, Tone=-25, Bass=2, Mid=1, HighpassHz=65 }),
        new("kid", "Маленький герой", "Голоса", "Ребяческий и звонкий", "Поднятая высота и облегчённые низкие частоты создают мультяшный детский характер.", "peach", "spark", new() { Pitch=7, Tone=45, Bass=-3, HighpassHz=100, DeEsser=35 }),
        new("helium", "Гелий", "Персонажи", "Очень высокий. Очень смешной", "Октава вверх для нарочито комичного голоса. Экстремальный сдвиг специально звучит неестественно.", "lime", "balloon", new() { Pitch=12, Tone=35, Bass=-4, HighpassHz=110 }),
        new("clown", "Клоун", "Персонажи", "Мультфильм на вашей стороне", "Высокая подача и мягкий плавающий хор. Попробуйте чуть растягивать гласные.", "peach", "clown", new() { Pitch=8, Tone=15, ChorusMix=42, EchoMix=9, EchoDelayMs=100, EchoFeedback=10 }),
        new("giant", "Великан", "Персонажи", "Большой голос. Большая комната", "Низкий регистр и короткая реверберация для большого фантастического персонажа.", "sand", "mountain", new() { Pitch=-9, Tone=-40, Bass=3, ReverbMix=14, HighpassHz=55, CompressorRatio=4 }),
        new("robot", "Робот", "Персонажи", "Металл в каждой фразе", "Кольцевая модуляция добавляет механические обертоны. Частоту и силу робота можно настроить справа.", "sky", "robot", new() { RobotMix=82, RobotFrequency=70, Drive=12, Mid=2, Bass=-2 }),
        new("android", "Андроид", "Персонажи", "Синтетический, но живой", "Умеренная роботизация, пониженный тон и хор — более разборчивый футуристический голос.", "sage", "cpu", new() { Pitch=-2, RobotMix=35, RobotFrequency=105, ChorusMix=22, Treble=2 }),
        new("alien", "Пришелец", "Персонажи", "С другой частоты", "Светлый тон, кольцевая модуляция и хор для космического персонажа.", "lime", "orbit", new() { Pitch=4, Tone=40, RobotMix=40, RobotFrequency=140, ChorusMix=50, EchoMix=13, EchoDelayMs=160 }),
        new("ghost", "Призрак", "Пространство", "Голос с длинным следом", "Воздушная реверберация, лёгкий хор и тихое эхо. Лучше звучит на неторопливой речи.", "rose", "ghost", new() { Pitch=2, Tone=-15, ChorusMix=28, ReverbMix=40, EchoMix=18, EchoDelayMs=380, EchoFeedback=40 }),
        new("cave", "Эхо в пещере", "Пространство", "Фраза остаётся в воздухе", "Отчётливые повторы с тёплой реверберацией. Уменьшите обратную связь для более короткого хвоста.", "sand", "wave", new() { EchoMix=35, EchoDelayMs=340, EchoFeedback=45, ReverbMix=24, Tone=-15 }),
        new("radio", "Рация", "Эффекты", "Плотный переговорный канал", "Крутые срезы 500–2600 Гц, резонанс середины и жёсткое насыщение. Сила эффекта регулирует характер; «Количество рации» — примесь радиоканала.", "sage", "radio", new() { Radio=true,RadioAmount=85, Drive=45, Mid=3, CompressorRatio=8, CompressorThreshold=-30, CompressorMakeup=7,DeEsser=10 }),
        new("megaphone", "Мегафон", "Эффекты", "Внимание, говорит VoxLab", "Узкая полоса, сатурация и короткий повтор для громкоговорителя.", "peach", "megaphone", new() { Radio=true, Drive=60, Mid=5, EchoMix=15, EchoDelayMs=90, EchoFeedback=15, CompressorRatio=6 }),
        new("double", "Двойник", "Эффекты", "Один голос. Несколько слоёв", "Плавно меняющаяся задержка создаёт многослойный хор. Полезен для персонажей и творческой записи.", "sky", "double", new() { ChorusMix=65, ReverbMix=12, Tone=8 }),
        new("arcade", "8 бит", "Эффекты", "Голос игровой приставки", "Низкая разрядность и частота оцифровки: хрустящий цифровой голос с намеренно грубыми артефактами.", "lime", "cpu", new() { CrushMix=85,CrushBits=4,CrushRate=4200,Pitch=3,Drive=12 }),
        new("broken-radio", "Плохая связь", "Эффекты", "Сигнал пробивается с трудом", "Узкая рация, цифровые потери качества и ритмические провалы громкости. Новая модуляция не добавляет шума в тишине.", "peach", "radio", new() { Radio=true,RadioAmount=100,CrushMix=50,CrushBits=5,CrushRate=5500,TremoloMix=85,TremoloRate=14,CompressorRatio=7,CompressorThreshold=-28,CompressorMakeup=6 }),
        new("underwater", "Под водой", "Пространство", "Булькающий, текучий голос", "Медленный флэнжер, пониженный тон и приглушённые верхние частоты создают движущееся водное звучание.", "sky", "wave", new() { Pitch=-3,Tone=-55,Treble=-6,FlangerMix=85,FlangerRate=.7,TremoloMix=30,TremoloRate=3,ReverbMix=20 }),
        new("ufo", "Летающая тарелка", "Персонажи", "Вращающийся сигнал", "Широкий флэнжер, пульсация и металлические обертоны. Скорости движения доступны в стойке эффектов.", "lime", "orbit", new() { RobotMix=45,RobotFrequency=130,FlangerMix=75,FlangerRate=1.4,TremoloMix=55,TremoloRate=6,EchoMix=18,EchoDelayMs=170 }),
        new("villain", "Тёмный лорд", "Персонажи", "Низкий, зернистый, тяжёлый", "Понижение на семь полутонов, насыщение, цифровая шероховатость и короткая тяжёлая комната.", "rose", "mountain", new() { Pitch=-7,Tone=-35,Drive=38,CrushMix=28,CrushBits=7,CrushRate=10000,ReverbMix=25,EchoMix=10,EchoDelayMs=110 }),
        new("tape", "Старая кассета", "Эффекты", "Тёплая запись с плавающим тоном", "Приглушённые верха, сатурация, медленное движение флэнжера и лёгкая пульсация без искусственного шипения.", "sand", "broadcast", new() { Tone=-30,Treble=-5,Drive=32,FlangerMix=35,FlangerRate=.12,TremoloMix=22,TremoloRate=.8 }),
        new("pulse", "Пульсар", "Эффекты", "Голос, нарезанный в ритм", "Глубокое тремоло на восемь герц и короткое эхо. Управляйте ритмом и глубиной в стойке.", "peach", "gauge", new() { TremoloMix=100,TremoloRate=8,EchoMix=20,EchoDelayMs=120,EchoFeedback=18 }),
        new("cathedral", "Собор", "Пространство", "Большое пространство за голосом", "Выраженная реверберация, разнесённые повторы и тихий хор. Подходит для медленных реплик и озвучки.", "rose", "ghost", new() { ReverbMix=65,EchoMix=28,EchoDelayMs=540,EchoFeedback=55,ChorusMix=20,Tone=-8 })
    ];
}
