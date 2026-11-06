namespace VoxLab.Core.Dsp;

internal sealed class DelayLine(int length)
{
    private readonly float[] buffer = new float[length];
    private int position;
    public float Read(double delay)
    {
        double index=position-Math.Clamp(delay,1,buffer.Length-2);
        if(index<0) index+=buffer.Length;
        int i=(int)index; double fraction=index-i;
        return (float)(buffer[i]*(1-fraction)+buffer[(i+1)%buffer.Length]*fraction);
    }
    public void Write(float sample) { buffer[position]=sample; position=(position+1)%buffer.Length; }
    public void Clear() { Array.Clear(buffer); position=0; }
}

internal sealed class Reverb
{
    private readonly float[][] combs;
    private readonly int[] indices=new int[4];
    private readonly float[] damping=new float[4];
    private readonly DelayLine allpass1, allpass2;
    private readonly int delay1, delay2;
    public Reverb(int rate)
    {
        combs=new[] { .0297,.0371,.0411,.0437 }.Select(t=>new float[(int)(rate*t)]).ToArray();
        delay1=(int)(rate*.005); delay2=(int)(rate*.0017);
        allpass1=new DelayLine(delay1+3); allpass2=new DelayLine(delay2+3);
    }
    public float Process(float x)
    {
        float sum=0;
        for(int i=0;i<4;i++)
        {
            float delayed=combs[i][indices[i]];
            damping[i]=.68f*delayed+.32f*damping[i];
            combs[i][indices[i]]=x*.25f+damping[i]*.76f;
            indices[i]=(indices[i]+1)%combs[i].Length;
            sum+=delayed;
        }
        float d1=allpass1.Read(delay1), y1=d1-.5f*sum;
        allpass1.Write(sum+.5f*y1);
        float d2=allpass2.Read(delay2), y2=d2-.5f*y1;
        allpass2.Write(y1+.5f*y2);
        return y2;
    }
    public void Clear() { foreach(var comb in combs) Array.Clear(comb); Array.Clear(indices); Array.Clear(damping); allpass1.Clear(); allpass2.Clear(); }
}
