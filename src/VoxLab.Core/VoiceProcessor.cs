using SoundTouch;
using VoxLab.Core.Dsp;

namespace VoxLab.Core;

/// <summary>Single-audio-thread streaming DSP. Allocates only at construction / parameter changes.</summary>
public sealed class VoiceProcessor
{
    public int SampleRate { get; }
    private VoiceSettings settings=new();
    private readonly SoundTouchProcessor pitch;
    private readonly Biquad highpass=new(), bass=new(), mid=new(), treble=new(), sibilants=new(), radioHigh=new(), radioLow=new(), radioHigh2=new(), radioLow2=new(), radioPresence=new();
    private readonly DelayLine echo, chorus, flanger;
    private readonly Reverb reverb;
    private readonly float[] scratch=new float[32768];
    private double gateEnvelope, gateGain=1, compEnvelope, compGain=1, deEssEnvelope, limiterGain=1;
    private double inGain=1, outGain=.708, robotPhase, chorusPhase, echoDelay;
    private int gateHold;
    private int compHold, compHoldSamples,gateHoldSamples;
    private double gatePeakRelease,rmsCoefficient;
    private bool gateOpen=true, previousBypass;
    private bool configured,colorActive;
    private double gateAttack, gateRelease, compAttack, compRelease, smooth;
    private double targetIn, targetOut, gateOpenLevel, gateCloseLevel, makeup, robotWet, echoWet, echoFeedback, reverbWet, chorusWet, drive;
    private double radioWet,crushWet,crushScale,crushInterval,crushClock,tremoloWet,tremoloPhase,flangerWet,flangerPhase;
    private float crushHeld;
    public bool Bypass { get; set; }
    public bool Muted { get; set; }
    public float InputPeak { get; private set; }
    public float OutputPeak { get; private set; }
    public float InputRms { get; private set; }
    public float OutputRms { get; private set; }
    public double GainReductionDb { get; private set; }
    public bool GateOpen => !settings.GateEnabled || gateOpen;
    public long ClippedInputSamples { get; private set; }
    public double AlgorithmLatencyMs => !Bypass && Math.Abs(settings.Pitch)>.001 ? pitch.GetSetting(SettingId.InitialLatency)*1000.0/SampleRate : 0;

    public VoiceProcessor(int sampleRate=48000)
    {
        if (sampleRate<8000 || sampleRate>192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SampleRate=sampleRate;
        pitch=new SoundTouchProcessor { SampleRate=sampleRate, Channels=1 };
        pitch.SetSetting(SettingId.SequenceDurationMs,32);
        pitch.SetSetting(SettingId.SeekWindowDurationMs,12);
        pitch.SetSetting(SettingId.OverlapDurationMs,8);
        pitch.SetSetting(SettingId.UseQuickSeek,0);
        pitch.SetSetting(SettingId.UseAntiAliasFilter,1);
        echo=new DelayLine(sampleRate+4); chorus=new DelayLine(sampleRate/10+4); flanger=new DelayLine(sampleRate/20+4); reverb=new Reverb(sampleRate);
        Apply(settings);
    }

    public void Apply(VoiceSettings value)
    {
        var next=value.Effective();
        if(configured && next==settings) return;
        if(settings.HighpassEnabled!=next.HighpassEnabled) highpass.Clear();
        if(settings.EqualizerEnabled!=next.EqualizerEnabled || next.Tone!=settings.Tone && next.Tone==0) { bass.Clear();mid.Clear();treble.Clear(); }
        if(settings.DeEsserEnabled!=next.DeEsserEnabled) { sibilants.Clear();deEssEnvelope=0; }
        if(chorusWet>0 && next.ChorusMix==0) chorus.Clear();
        if(echoWet>0 && next.EchoMix==0) echo.Clear();
        if(reverbWet>0 && next.ReverbMix==0) reverb.Clear();
        if(flangerWet>0 && next.FlangerMix==0) flanger.Clear();
        if(next.CrushMix==0) { crushHeld=0; crushClock=0; }
        if(echoWet==0) echoDelay=next.EchoDelayMs*SampleRate/1000;
        colorActive=next.Tone!=0 || next.EqualizerEnabled && (next.Bass!=0 || next.Mid!=0 || next.Treble!=0);
        if((Math.Abs(next.Pitch)<.001)!=(Math.Abs(settings.Pitch)<.001)) pitch.Clear();
        if(!configured || settings.Pitch!=next.Pitch) pitch.PitchSemiTones=next.Pitch;
        settings=next; configured=true;
        highpass.Configure("hp",settings.HighpassHz,0,SampleRate);
        bass.Configure("low",180,(settings.EqualizerEnabled?settings.Bass:0)-settings.Tone*.045,SampleRate);
        mid.Configure("peak",1400,settings.EqualizerEnabled?settings.Mid:0,SampleRate,.85);
        treble.Configure("high",3600,(settings.EqualizerEnabled?settings.Treble:0)+settings.Tone*.045,SampleRate);
        sibilants.Configure("hp",5200,0,SampleRate);
        radioHigh.Configure("hp",500,0,SampleRate); radioHigh2.Configure("hp",500,0,SampleRate);
        radioLow.Configure("lp",2600,0,SampleRate); radioLow2.Configure("lp",2600,0,SampleRate);
        radioPresence.Configure("peak",1550,7,SampleRate,1.2);
        radioWet=settings.Radio ? settings.RadioAmount/100 : 0;
        crushWet=settings.CrushMix/100; crushScale=Math.Pow(2,Math.Round(settings.CrushBits)-1);
        crushInterval=Math.Max(1,SampleRate/settings.CrushRate); tremoloWet=settings.TremoloMix/100;flangerWet=settings.FlangerMix/100;
        targetIn=DbToLinear(settings.InputGain); targetOut=DbToLinear(settings.OutputGain);
        gateOpenLevel=DbToLinear(settings.GateThreshold); gateCloseLevel=DbToLinear(settings.GateThreshold-4);
        gateAttack=Coefficient(settings.GateAttackMs); gateRelease=Coefficient(settings.GateReleaseMs);
        compAttack=Coefficient(settings.CompressorAttackMs); compRelease=Coefficient(settings.CompressorReleaseMs);
        gatePeakRelease=Coefficient(18); rmsCoefficient=Coefficient(8);
        gateHoldSamples=(int)(settings.GateHoldMs*SampleRate/1000); compHoldSamples=SampleRate*45/1000;
        if(!settings.CompressorEnabled) { compGain=1; compEnvelope=0; compHold=0; GainReductionDb=0; }
        if(!settings.LimiterEnabled) limiterGain=1;
        makeup=DbToLinear(settings.CompressorMakeup); smooth=Coefficient(12);
        robotWet=settings.RobotMix/100; echoWet=settings.EchoMix/100; echoFeedback=settings.EchoFeedback/100;
        reverbWet=settings.ReverbMix/100; chorusWet=settings.ChorusMix/100*.65; drive=settings.Drive/100;
        if(echoDelay==0) echoDelay=settings.EchoDelayMs*SampleRate/1000;
    }

    /// <summary>Output capacity must be >= 32768. Output counts vary while the pitch engine batches samples.</summary>
    public int Process(ReadOnlySpan<float> input, Span<float> output)
    {
        if(input.Length>scratch.Length) throw new ArgumentException("Audio block is too large.",nameof(input));
        if(output.Length<scratch.Length) throw new ArgumentException("Output needs 32768 samples.",nameof(output));
        if(Bypass!=previousBypass) { pitch.Clear(); previousBypass=Bypass; }
        float inputPeak=0; double inputEnergy=0;
        for(int i=0;i<input.Length;i++)
        {
            float raw=float.IsFinite(input[i]) ? Math.Clamp(input[i],-8,8) : 0;
            inGain=smooth*inGain+(1-smooth)*targetIn;
            float x=(float)(raw*inGain);
            inputPeak=Math.Max(inputPeak,Math.Abs(x)); inputEnergy+=x*x;
            if(Math.Abs(raw)>=.999) ClippedInputSamples++;
            if(!Bypass) x=PreProcess(x);
            scratch[i]=x;
        }
        InputPeak=inputPeak; InputRms=(float)Math.Sqrt(inputEnergy/Math.Max(1,input.Length));
        int count;
        if(!Bypass && Math.Abs(settings.Pitch)>.001)
        {
            pitch.PutSamples(scratch.AsSpan(0,input.Length),input.Length);
            count=pitch.ReceiveSamples(output,output.Length);
        }
        else { scratch.AsSpan(0,input.Length).CopyTo(output); count=input.Length; }
        Finish(output[..count]);
        return count;
    }

    public int Flush(Span<float> output)
    {
        if(Bypass || Math.Abs(settings.Pitch)<.001) return 0;
        pitch.Flush(); int count=pitch.ReceiveSamples(output,output.Length); Finish(output[..count]); return count;
    }

    private float PreProcess(float input)
    {
        float x=settings.HighpassEnabled?highpass.Process(input):input;
        // Fast peak envelope for gate detection, with hysteresis and a hold window.
        gateEnvelope=Math.Max(Math.Abs(x),gateEnvelope*gatePeakRelease);
        if(gateEnvelope>=gateOpenLevel) { gateOpen=true; gateHold=gateHoldSamples; }
        else if(gateEnvelope<gateCloseLevel) { if(gateHold>0) gateHold--; else gateOpen=false; }
        // Expand gradually below the closing threshold: retain quiet consonants
        // near the threshold, while still attenuating a distant noise floor.
        double tail=Math.Min(1,gateEnvelope/gateCloseLevel); tail*=tail;
        double gateTarget=!settings.GateEnabled || gateOpen ? 1 : Math.Max(.0003,tail*tail);
        double gc=gateTarget>gateGain ? gateAttack : gateRelease;
        gateGain=gc*gateGain+(1-gc)*gateTarget;
        if(settings.GateEnabled) x*=(float)gateGain;
        if(colorActive) x=treble.Process(mid.Process(bass.Process(x)));
        if(settings.DeEsserEnabled && settings.DeEsser>0) {
            float high=sibilants.Process(x);
            deEssEnvelope=.995*deEssEnvelope+.005*high*high;
            double essReduction=Math.Clamp((Math.Sqrt(deEssEnvelope)-.018)*12,0,.85)*settings.DeEsser/100;
            x-=(float)(high*essReduction);
        }
        if(!settings.CompressorEnabled) return x;
        // Measure mean square symmetrically. Attack/release belong to the gain
        // envelope, not the individual rising/falling halves of each waveform.
        compEnvelope=rmsCoefficient*compEnvelope+(1-rmsCoefficient)*x*x;
        double over=10*Math.Log10(compEnvelope+1e-16)-settings.CompressorThreshold;
        const double knee=6;
        double reduction=over<-knee/2 ? 0 : over>knee/2 ? (1-1/settings.CompressorRatio)*over : (1-1/settings.CompressorRatio)*Math.Pow(over+knee/2,2)/(2*knee);
        double desired=DbToLinear(-reduction);
        if(desired<compGain) { compHold=compHoldSamples; compGain=compAttack*compGain+(1-compAttack)*desired; }
        else if(compHold>0) compHold--;
        else compGain=compRelease*compGain+(1-compRelease)*desired;
        return x*(float)(compGain*makeup);
    }

    private void Finish(Span<float> samples)
    {
        GainReductionDb=settings.CompressorEnabled && !Bypass ? -LinearToDb(compGain) : 0;
        if(samples.IsEmpty) return; // A batched pitch callback has no new output meter reading.
        float peak=0; double energy=0;
        double limiterRelease=Coefficient(90);
        for(int i=0;i<samples.Length;i++)
        {
            float x=samples[i];
            if(!Bypass)
            {
                if(robotWet>0) {
                    robotPhase+=2*Math.PI*settings.RobotFrequency/SampleRate;
                    if(robotPhase>2*Math.PI) robotPhase-=2*Math.PI;
                    x=(float)(x*(1-robotWet)+x*Math.Cos(robotPhase)*robotWet*1.6);
                }
                if(drive>0) x=(float)(Math.Tanh(x*(1+drive*9))/(1+drive*1.7));
                if(radioWet>0)
                {
                    float band=radioPresence.Process(radioHigh2.Process(radioHigh.Process(x)));
                    // Saturate inside the band and filter again afterwards, keeping
                    // distortion harmonics inside the narrow communications channel.
                    float radio=radioLow2.Process(radioLow.Process((float)(Math.Tanh(band*7)*.48)));
                    x=(float)(x*(1-radioWet)+radio*radioWet);
                }
                if(crushWet>0)
                {
                    if(crushClock<=0) { crushHeld=(float)(Math.Round(x*crushScale)/crushScale);crushClock+=crushInterval; }
                    crushClock--;x=(float)(x*(1-crushWet)+crushHeld*crushWet);
                }
                if(tremoloWet>0)
                {
                    tremoloPhase+=2*Math.PI*settings.TremoloRate/SampleRate;if(tremoloPhase>2*Math.PI)tremoloPhase-=2*Math.PI;
                    x*=(float)(1-tremoloWet*.5*(1-Math.Sin(tremoloPhase)));
                }
                if(flangerWet>0)
                {
                    flangerPhase+=2*Math.PI*settings.FlangerRate/SampleRate;if(flangerPhase>2*Math.PI)flangerPhase-=2*Math.PI;
                    float sweep=flanger.Read(SampleRate*(.0025+.002*Math.Sin(flangerPhase)));
                    flanger.Write(Math.Clamp(x+sweep*.35f,-2,2)); x=(float)(x*(1-flangerWet*.35)+sweep*flangerWet*.7);
                }
                if(chorusWet>0) {
                    chorusPhase+=2*Math.PI*.72/SampleRate; if(chorusPhase>2*Math.PI) chorusPhase-=2*Math.PI;
                    float doubled=(chorus.Read(SampleRate*(.014+.004*Math.Sin(chorusPhase)))+chorus.Read(SampleRate*(.021+.005*Math.Cos(chorusPhase*.83))))*.5f;
                    chorus.Write(x); x=(float)(x*(1-chorusWet*.4)+doubled*chorusWet);
                }
                if(echoWet>0) {
                    echoDelay=.9995*echoDelay+.0005*settings.EchoDelayMs*SampleRate/1000;
                    float delayed=echo.Read(echoDelay); echo.Write(Math.Clamp(x+delayed*(float)echoFeedback,-4,4));
                    x+=(float)(delayed*echoWet);
                }
                if(reverbWet>0) { float room=reverb.Process(x); x=(float)(x*(1-reverbWet*.25)+room*reverbWet); }
            }
            outGain=smooth*outGain+(1-smooth)*targetOut;
            double value=x*outGain;
            if(!double.IsFinite(value)) value=0;
            if(settings.LimiterEnabled) {
                double needed=Math.Abs(value)>.95 ? .95/Math.Abs(value) : 1;
                limiterGain=needed<limiterGain ? needed : limiterRelease*limiterGain+(1-limiterRelease)*needed;
                value=Math.Clamp(value*limiterGain,-.95,.95);
            }
            float result=Muted ? 0 : (float)Math.Clamp(value,-1,1);
            samples[i]=result; peak=Math.Max(peak,Math.Abs(result)); energy+=result*result;
        }
        OutputPeak=peak; OutputRms=(float)Math.Sqrt(energy/Math.Max(1,samples.Length));
    }
    private double Coefficient(double milliseconds) => Math.Exp(-1/(SampleRate*milliseconds*.001));
    public static double DbToLinear(double decibels) => Math.Pow(10,decibels/20);
    public static double LinearToDb(double value) => Math.Max(-90,20*Math.Log10(Math.Max(1e-9,value)));
}
