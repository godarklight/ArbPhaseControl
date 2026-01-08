using System;
using System.Threading;
using Gtk;

namespace ArbPhaseControl
{
    class Program
    {
        static bool running = true;

        [STAThread]
        public static void Main(string[] args)
        {
            IAudioDriver ad = null;
            if (Settings.ENABLE_AUDIO)
            {
                ad = new TCPAudio();
                //ad = new AudioDriver();
            }

            Connection c = new Connection();
            DisplayWorker dw = new DisplayWorker(c);

            Application.Init();

            var app = new Application("org.ArbPhaseControl.ArbPhaseControl", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow(c, dw);
            app.AddWindow(win);

            win.Show();
            ad?.Start();
            c.Start(ad);
            Application.Run();
            running = false;
            ad?.Stop();
            c.Stop();
        }
    }
}
