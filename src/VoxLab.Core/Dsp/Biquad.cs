namespace VoxLab.Core.Dsp;

/// <summary>RBJ audio-EQ cookbook filters, transposed direct form II.</summary>
internal sealed class Biquad
{
    private double b0=1, b1, b2, a1, a2, z1, z2;
    public float Process(float input)
    {
        double output=b0*input+z1;
        z1=b1*input-a1*output+z2;
        z2=b2*input-a2*output;
        if (Math.Abs(z1)<1e-24) z1=0;
        if (Math.Abs(z2)<1e-24) z2=0;
        return (float)output;
    }
    public void Configure(string kind, double frequency, double gain, int rate, double q=0.707)
    {
        double w=2*Math.PI*Math.Clamp(frequency,10,rate*.45)/rate, c=Math.Cos(w), s=Math.Sin(w);
        double alpha=s/(2*q), a=Math.Pow(10,gain/40), root=2*Math.Sqrt(a)*alpha;
        double n0, n1, n2, d0, d1, d2;
        switch(kind)
        {
            case "hp": n0=(1+c)/2; n1=-(1+c); n2=n0; d0=1+alpha; d1=-2*c; d2=1-alpha; break;
            case "lp": n0=(1-c)/2; n1=1-c; n2=n0; d0=1+alpha; d1=-2*c; d2=1-alpha; break;
            case "peak": n0=1+alpha*a; n1=-2*c; n2=1-alpha*a; d0=1+alpha/a; d1=n1; d2=1-alpha/a; break;
            case "low":
                n0=a*((a+1)-(a-1)*c+root); n1=2*a*((a-1)-(a+1)*c); n2=a*((a+1)-(a-1)*c-root);
                d0=(a+1)+(a-1)*c+root; d1=-2*((a-1)+(a+1)*c); d2=(a+1)+(a-1)*c-root; break;
            default:
                n0=a*((a+1)+(a-1)*c+root); n1=-2*a*((a-1)+(a+1)*c); n2=a*((a+1)+(a-1)*c-root);
                d0=(a+1)-(a-1)*c+root; d1=2*((a-1)-(a+1)*c); d2=(a+1)-(a-1)*c-root; break;
        }
        b0=n0/d0; b1=n1/d0; b2=n2/d0; a1=d1/d0; a2=d2/d0;
    }
}
