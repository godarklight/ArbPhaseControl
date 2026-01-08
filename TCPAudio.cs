using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;

class TCPAudio : IAudioDriver
{
    TcpClient c = new TcpClient(AddressFamily.InterNetworkV6);
    byte[] outdata = new byte[1024 * 4];

    public TCPAudio()
    {
        try
        {
            c.Connect(IPAddress.IPv6Loopback, 12345);
        }
        catch
        {
            Console.WriteLine("Audio connection failed");
            c = null;
        }
    }
    public void Write(float[] data)
    {
        if (c == null)
        {
            return;
        }
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
            BitConverter.GetBytes(newSample).CopyTo(outdata, i * 4);
        }
        try
        {
            c.GetStream().Write(outdata, 0, 4096);
        }
        catch
        {
            Console.WriteLine("Audio connection failed");
            c = null;
        }
    }

    public void Start()
    {

    }

    public void Stop()
    {
        c?.Dispose();
        c = null;
    }
}