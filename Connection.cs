//TO ENABLE AUDIO, EDIT AUDIOSETTING.CS

using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Serialization.Formatters;
using System.Threading;

class Connection
{
    TcpClient client = new TcpClient(AddressFamily.InterNetworkV6);
    byte[] readBuffer = new byte[1024 * 8];
    byte[] sendBuffer = new byte[8];
    Thread readThread;
    bool running = true;
    float gain;
    float phase;
    bool send = false;
    Action<Complex[], Complex[]> callback;
    Complex[] antenna1 = new Complex[1024];
    Complex[] antenna2 = new Complex[1024];
    const double crystal = 7118769;
    Vfo vfo = new Vfo(96000);
    IAudioDriver ad;
    float[] audioData = new float[1024];
    double audioAgc = 1.0;
    int hold_time = 0;
    AutoResetEvent fft1Event = new AutoResetEvent(false);
    AutoResetEvent fft2Event = new AutoResetEvent(false);
    AutoResetEvent fft1Done = new AutoResetEvent(false);
    AutoResetEvent fft2Done = new AutoResetEvent(false);
    Thread fft1Thread;
    Thread fft2Thread;
    Complex[] ant1IFFT;
    Complex[] ant2IFFT;

    public void Start(IAudioDriver ad)
    {
        this.ad = ad;
        if (Settings.ENABLE_AUDIO)
        {
            antenna1 = new Complex[2048];
            antenna2 = new Complex[2048];
        }
        //client.Connect(IPAddress.Loopback, 5797);
        //client.Connect(IPEndPoint.Parse("[2403:580c:7a4a::43]:5797"));
        client.Connect(IPEndPoint.Parse("[2403:580c:7a4a::5]:5797"));
        readThread = new Thread(new ThreadStart(ConnectionLoop));
        readThread.Name = "ConnectionLoop";
        readThread.Start();
        fft1Thread = new Thread(new ThreadStart(FFT1Thread));
        fft1Thread.Name = "FFT1";
        fft1Thread.Start();
        fft2Thread = new Thread(new ThreadStart(FFT2Thread));
        fft2Thread.Name = "FFT2";
        fft2Thread.Start();
        //Reset arbphase to match UI
        Send(0, 0);
    }

    public void Register(Action<Complex[], Complex[]> callback)
    {
        this.callback = callback;
    }

    public void Send(float gain, float phase)
    {
        this.phase = phase;
        this.gain = gain;
        send = true;
    }

    public void SetFrequency(float frequency)
    {
        vfo.SetFrequency(crystal - frequency);
    }

    public void ConnectionLoop()
    {
        while (running)
        {
            while (client.Available >= 8192)
            {
                int bytesToRead = 8192;
                while (bytesToRead > 0)
                {
                    int readBytes = client.GetStream().Read(readBuffer, bytesToRead - 8192, bytesToRead);
                    if (readBytes == 0)
                    {
                        Console.WriteLine("Disconnected");
                        break;
                    }
                    bytesToRead -= readBytes;
                }
                for (int i = 0; i < 1024; i++)
                {
                    float real1 = BitConverter.ToInt16(readBuffer, i * 8) / (float)short.MaxValue;
                    float imaginary1 = BitConverter.ToInt16(readBuffer, i * 8 + 2) / (float)short.MaxValue;
                    float real2 = BitConverter.ToInt16(readBuffer, i * 8 + 4) / (float)short.MaxValue;
                    float imaginary2 = BitConverter.ToInt16(readBuffer, i * 8 + 6) / (float)short.MaxValue;
                    Complex vfoSample = vfo.GetSample();
                    //Overlap and save FFT
                    if (Settings.ENABLE_AUDIO)
                    {
                        antenna1[i] = antenna1[i + 1024];
                        antenna2[i] = antenna2[i + 1024];
                        antenna1[i + 1024] = new Complex(real1, imaginary1) * vfoSample;
                        antenna2[i + 1024] = new Complex(real2, imaginary2) * vfoSample;
                    }
                    else
                    {
                        antenna1[i] = new Complex(real1, imaginary1) * vfoSample;
                        antenna2[i] = new Complex(real2, imaginary2) * vfoSample;
                    }
                }
                if (!Settings.ENABLE_AUDIO)
                {
                    vfo.SkipSamples(4*1024);
                }
                fft1Event.Set();
                fft2Event.Set();
                fft1Done.WaitOne();
                fft2Done.WaitOne();
                /*
                ant1IFFT = antenna1;
                ant2IFFT = antenna2;
                */
                if (Settings.ENABLE_AUDIO)
                {
                    for (int i = 0; i < 1024; i++)
                    {
                        Complex audioSample = (ant1IFFT[i + 512] + ant2IFFT[i + 512]) * audioAgc;
                        if (audioSample.Magnitude < 0.1)
                        {
                            if (hold_time == 0)
                            {
                                audioAgc *= 1.00001;
                            }
                            else
                            {
                                hold_time -= 1;
                            }
                        }
                        else
                        {
                            hold_time = 1024;
                            audioAgc *= 0.99;
                        }
                        audioData[i] = (float)audioSample.Real;
                    }
                    if (ad != null)
                    {
                        ad.Write(audioData);
                    }
                }
                callback(ant1IFFT, ant2IFFT);
            }
            if (send)
            {
                send = false;
                BitConverter.GetBytes(gain).CopyTo(sendBuffer, 0);
                BitConverter.GetBytes(phase).CopyTo(sendBuffer, 4);
                client.GetStream().Write(sendBuffer, 0, 8);
            }
            Thread.Sleep(1);
        }
    }

    private void FFT1Thread()
    {
        int lowFreqBin = (300 * antenna1.Length) / 96000;
        int highFreqBin = (2000 * antenna1.Length) / 96000;
        //Swap to LSB negative frequencies
        lowFreqBin = antenna1.Length - lowFreqBin;
        highFreqBin = antenna1.Length - highFreqBin;
        while (running)
        {
            if (fft1Event.WaitOne(10))
            {
                Complex[] ant1FFT = new Complex[antenna1.Length];
                antenna1.CopyTo(ant1FFT, 0);
                FftSharp.FFT.Forward(ant1FFT);
                for (int i = 0; i < antenna1.Length; i++)
                {
                    if (i < highFreqBin || i > lowFreqBin)
                    {
                        ant1FFT[i] = Complex.Zero;
                    }
                }
                FftSharp.FFT.Inverse(ant1FFT);
                ant1IFFT = ant1FFT;
                fft1Done.Set();
            }
        }
    }
    private void FFT2Thread()
    {
        int lowFreqBin = (300 * antenna1.Length) / 96000;
        int highFreqBin = (2000 * antenna1.Length) / 96000;
        //Swap to LSB negative frequencies
        lowFreqBin = antenna1.Length - lowFreqBin;
        highFreqBin = antenna1.Length - highFreqBin;
        while (running)
        {
            if (fft2Event.WaitOne(10))
            {
                Complex[] ant2FFT = new Complex[antenna2.Length];
                antenna2.CopyTo(ant2FFT, 0);
                FftSharp.FFT.Forward(ant2FFT);
                for (int i = 0; i < antenna2.Length; i++)
                {
                    if (i < highFreqBin || i > lowFreqBin)
                    {
                        ant2FFT[i] = Complex.Zero;
                    }
                }
                FftSharp.FFT.Inverse(ant2FFT);
                ant2IFFT = ant2FFT;
                fft2Done.Set();
            }
        }
    }


    public void Stop()
    {
        running = false;
        Console.WriteLine("Exiting");
    }
}