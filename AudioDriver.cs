using System;
using System.Threading;
using System.Numerics;
using PortAudioSharp;
using Stream = PortAudioSharp.Stream;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using Cairo;

class AudioDriver : IAudioDriver
{
    float[] audioOut = new float[1024];
    ConcurrentQueue<float[]> buffers = new ConcurrentQueue<float[]>();
    int readPos = 0;
    AutoResetEvent audioReady = new AutoResetEvent(true);
    AutoResetEvent dataReady = new AutoResetEvent(true);
    object lockObject = new object();
    bool started = false;
    Random r = new Random();

    //Debug
    bool running = true;

    public void Start()
    {
        PortAudio.Initialize();
        int selectedDevice = PortAudio.DefaultOutputDevice;
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {

            DeviceInfo di = PortAudio.GetDeviceInfo(i);
            if (i == PortAudio.DefaultOutputDevice)
            {
                Console.Write("DEFAULT = ");
            }
            Console.WriteLine(di.name);
            if (di.name.Contains("CORSAIR"))
            {
                //selectedDevice = i;
            }
        }
        StreamParameters outParam = new StreamParameters();
        outParam.channelCount = 2;
        outParam.device = selectedDevice;
        outParam.sampleFormat = SampleFormat.Float32;
        outParam.suggestedLatency = 0.05;
        Stream audioStream = new Stream(null, outParam, 96000, 0, StreamFlags.NoFlag, AudioCallbackRandom, null);
        audioStream.Start();
    }

    public StreamCallbackResult AudioCallbackRandom(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userDataPtr)
    {
        unsafe
        {
            float* floatOut = (float*)output.ToPointer();
            for (int i = 0; i < frameCount; i++)
            {
                *floatOut++ = (float)(r.NextDouble() * short.MaxValue * 0.01);
                *floatOut++ = (float)(r.NextDouble() * short.MaxValue * 0.01);
            }
        }
        return StreamCallbackResult.Continue;
    }



    public StreamCallbackResult AudioCallback(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userDataPtr)
    {
        //Let data build up
        if (!started)
        {
            if (buffers.Count < 8)
            {
                return StreamCallbackResult.Continue;
            }
            else
            {
                started = true;
            }
        }
        unsafe
        {
            float* floatOut = (float*)output.ToPointer();
            for (int i = 0; i < frameCount; i++)
            {
                if (readPos == 1024)
                {
                    if (buffers.TryDequeue(out float[] newBuffer))
                    {
                        readPos = 0;
                        audioOut = newBuffer;
                    }
                    else
                    {
                        readPos = 0;
                    }
                }
                *floatOut++ = audioOut[readPos];
                *floatOut++ = audioOut[readPos];
                readPos++;
            }
        }

        if (!running)
        {
            return StreamCallbackResult.Complete;
        }
        return StreamCallbackResult.Continue;
    }

    public void Write(float[] data)
    {
        Console.WriteLine(buffers.Count);
        float[] newData = new float[1024];
        for (int i = 0; i < 1024; i++)
        {
            float newSample = data[i];
            if (newSample > 0.99f)
            {
                newSample = 0.99f;
            }
            if (newSample < -0.99f)
            {
                newSample = -0.99f;
            }
            newData[i] = newSample;
        }
        buffers.Enqueue(newData);
    }

    public void Stop()
    {
        Console.WriteLine("PA stopped");
        running = false;
        PortAudio.Terminate();
    }
}