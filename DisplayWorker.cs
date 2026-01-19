using System;
using System.Numerics;
using GLib;
using Gtk;


class DisplayWorker
{
    public const int WIDTH_HEIGHT = 768;
    const int pixels = 3;
    const int row = WIDTH_HEIGHT * pixels;
    const int brightness = 100;

    byte[] currentData = new byte[WIDTH_HEIGHT * row];
    byte[] nextData = new byte[WIDTH_HEIGHT * row];
    Connection connection;
    Action<byte[], double, double, double> callback;
    double agc = 1.0;
    double agcmax = 100000;
    double channel1Level = 0;
    double channel2Level = 0;
    double combinedLevel = 0;
    Vfo khzoffset = new Vfo(96000);
    public Complex averagePhase = Complex.Zero;


    public DisplayWorker(Connection connection)
    {
        this.connection = connection;
        connection.Register(ReceiveData);
        khzoffset.SetFrequency(1000);
    }

    public void Register(Action<byte[], double, double, double> callback)
    {
        this.callback = callback;
    }


    private void DrawBlankDisplay()
    {
        if (Settings.ENABLE_AUDIO)
        {
            for (int i = 0; i < nextData.Length; i++)
            {
                nextData[i] = (byte)(currentData[i] * 0.9);
            }
        }
        else
        {
            for (int i = 0; i < nextData.Length; i++)
            {
                nextData[i] = (byte)(currentData[i] * 0.5);
            }
        }
        //Marker ticks
        for (int i = 0; i < 32; i++)
        {
            //Real ticks
            nextData[((WIDTH_HEIGHT / 2 - 16) + i) * row + (WIDTH_HEIGHT / 4) * pixels + 1] = 64;
            nextData[((WIDTH_HEIGHT / 2 - 16) + i) * row + (3 * WIDTH_HEIGHT / 4) * pixels + 1] = 64;
            //Imaginary ticks
            nextData[row * (WIDTH_HEIGHT / 4) + (WIDTH_HEIGHT / 2 - 16) * pixels + i * pixels + 1] = 64;
            nextData[row * (3 * WIDTH_HEIGHT / 4) + (WIDTH_HEIGHT / 2 - 16) * pixels + i * pixels + 1] = 64;
        }
        for (int i = 0; i < WIDTH_HEIGHT; i++)
        {
            //Horizontal line
            nextData[row * (WIDTH_HEIGHT / 2) + i * pixels + 1] = 128;
            //Vertical line
            nextData[row * i + (WIDTH_HEIGHT / 2) * pixels + 1] = 128;
        }
    }

    private void DrawRedDot(Complex c)
    {
        int x = (int)((WIDTH_HEIGHT / 2) + (WIDTH_HEIGHT / 4) * c.Real);
        int y = (int)((WIDTH_HEIGHT / 2) + (WIDTH_HEIGHT / 4) * c.Imaginary);
        if (x < 0 || x > (WIDTH_HEIGHT - 1))
        {
            return;
        }
        if (y < 0 || y > (WIDTH_HEIGHT - 1))
        {
            return;
        }
        int pixelPos = y * row + x * pixels;
        int newValue = nextData[pixelPos] + brightness;
        if (newValue > 255)
        {
            newValue = 255;
        }
        nextData[pixelPos] = (byte)newValue;

    }
    private void DrawGreenDot(Complex c)
    {
        int x = (int)((WIDTH_HEIGHT / 2) + (WIDTH_HEIGHT / 4) * c.Real);
        int y = (int)((WIDTH_HEIGHT / 2) + (WIDTH_HEIGHT / 4) * c.Imaginary);
        if (x < 0 || x > (WIDTH_HEIGHT - 1))
        {
            return;
        }
        if (y < 0 || y > (WIDTH_HEIGHT - 1))
        {
            return;
        }
        int pixelPos = y * row + x * pixels + 1;
        int newValue = nextData[pixelPos] + brightness;
        if (newValue > 255)
        {
            newValue = 255;
        }
        nextData[pixelPos] = (byte)newValue;

    }

    private void DrawPhaseDot(Complex c)
    {
        int x = (int)((WIDTH_HEIGHT / 2) + (WIDTH_HEIGHT / 4) * c.Real);
        int y = (int)((WIDTH_HEIGHT / 2) + (WIDTH_HEIGHT / 4) * c.Imaginary);
        if (x < 0 || x >= (WIDTH_HEIGHT - 1))
        {
            return;
        }
        if (y < 0 || y >= (WIDTH_HEIGHT - 1))
        {
            return;
        }
        int pixelPos = y * row + x * pixels + 1;
        for (int i = 0; i < 3; i++)
        {
            int newValue = nextData[pixelPos] + brightness;
            if (newValue > 255)
            {
                newValue = 255;
            }
            nextData[pixelPos] = (byte)newValue;
            pixelPos++;
        }
    }

    private void ReceiveData(Complex[] antenna1, Complex[] antenna2)
    {
        DrawBlankDisplay();
        bool atReference = false;

        for (int i = 0; i < 1024; i++)
        {
            Complex vfo = khzoffset.GetSample();
            Complex ant1 = antenna1[i] * vfo;
            Complex ant2 = antenna2[i] * vfo;

            if (Settings.ENABLE_AUDIO)
            {
                channel1Level = 0.99999 * channel1Level + 0.00001 * ant1.Magnitude;
                channel2Level = 0.99999 * channel2Level + 0.00001 * ant2.Magnitude;
                combinedLevel = 0.99999 * combinedLevel + 0.00001 * (ant1 + ant2).Magnitude;
            }
            else
            {
                channel1Level = 0.9999 * channel1Level + 0.0001 * ant1.Magnitude;
                channel2Level = 0.9999 * channel2Level + 0.0001 * ant2.Magnitude;
                combinedLevel = 0.9999 * combinedLevel + 0.0001 * (ant1 + ant2).Magnitude;
            }


            ant1 = ant1 * agc;
            ant2 = ant2 * agc;
            if (ant1.Magnitude > 1.0)
            {
                atReference = true;
            }
            if (ant2.Magnitude > 1.0)
            {
                atReference = true;
            }
            if (ant1.Magnitude > 2.0)
            {
                agc = agc / ant1.Magnitude;
            }
            if (ant2.Magnitude > 2.0)
            {
                agc = agc / ant2.Magnitude;
            }
            if (i > 256 && i < 512)
            {
                DrawRedDot(ant1);
                DrawGreenDot(ant2);
                Complex newPhaseDot = ant1 * Complex.Conjugate(ant2);
                if (Settings.ENABLE_AUDIO)
                {
                    averagePhase = averagePhase * 0.99 + newPhaseDot * 0.01;
                }
                else
                {
                    averagePhase = averagePhase * 0.9 + newPhaseDot * 0.1;
                }
                DrawPhaseDot(newPhaseDot);
            }
        }
        if (!Settings.ENABLE_AUDIO)
        {
            khzoffset.SkipSamples(1024 * 4);
        }
        if (Settings.ENABLE_AUDIO)
        {
            if (!atReference)
            {
                agc *= 1.01;
            }
            else
            {
                agc *= 0.99;
            }
        }
        else
        {
            if (!atReference)
            {
                agc *= 1.03;
            }
            else
            {
                agc *= 0.97;
            }
        }

        //Swap buffer and call
        byte[] temp = currentData;
        currentData = nextData;
        nextData = temp;
        callback(currentData, channel1Level, channel2Level, combinedLevel);
    }
}