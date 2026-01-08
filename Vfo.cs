using System;
using System.Numerics;

class Vfo
{
    double sampleRate;
    double tuning = 0;
    double phase = 0;

    public Vfo(double sampleRate)
    {
        this.sampleRate = sampleRate;
    }

    public void SetFrequency(double frequency)
    {
        tuning = Math.Tau * frequency / sampleRate;
    }

    public void SkipSamples(int samples)
    {
        phase += tuning * samples;
        phase = phase % Math.Tau;
    }

    public Complex GetSample()
    {
        Complex returnValue = new Complex(Math.Cos(phase), Math.Sin(phase));
        //Keep phase around 0
        phase += tuning;
        if (phase > Math.Tau)
        {
            phase -= Math.Tau;
        }
        if (phase < -Math.Tau)
        {
            phase += Math.Tau;
        }
        return returnValue;
    }
}