interface IAudioDriver
{
    void Start();
    void Stop();
    void Write(float[] data);
}