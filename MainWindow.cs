using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;



using System.Threading;
using System.Net.Sockets;
using System.Net;
using Image = Gtk.Image;
using Gdk;
using System.Diagnostics;
using System.Numerics;

namespace ArbPhaseControl
{
    class MainWindow : Gtk.Window
    {
        [UI] private SpinButton spin_phase = null;
        [UI] private SpinButton spin_gain = null;
        [UI] private SpinButton spin_vfo = null;
        [UI] private Image iqDisplay = null;
        [UI] private EventBox imageBox = null;
        [UI] private Label channel1Level = null;
        [UI] private Label channel2Level = null;
        [UI] private Label combinedLevel = null;

        bool running = true;
        bool send = false;
        float gain = 0;
        float phase = 0;
        Connection connection;
        DisplayWorker displayWorker;
        double level1db = -140;
        double level2db = -140;


        public MainWindow(Connection connection, DisplayWorker displayWorker) : this(new Builder("MainWindow.glade"))
        {
            this.connection = connection;
            this.displayWorker = displayWorker;
            displayWorker.Register(DrawMain);
            connection.SetFrequency(7132000);
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);
            spin_gain.Changed += GainChanged;
            spin_gain.ValueChanged += GainChanged;
            spin_phase.Changed += PhaseChanged;
            spin_phase.ValueChanged += PhaseChanged;
            spin_vfo.Changed += VFOChanged;
            spin_vfo.ValueChanged += VFOChanged;
            DeleteEvent += Window_DeleteEvent;
            imageBox.ButtonPressEvent += ClickEvent;

        }

        private void ClickEvent(object sender, ButtonPressEventArgs args)
        {
            uint mouseButton = args.Event.Button;
            Console.WriteLine("Click " + mouseButton);
            double midpoint = DisplayWorker.WIDTH_HEIGHT / 2.0;
            double clickx = (args.Event.X - midpoint) / midpoint;
            double clicky = -(args.Event.Y - midpoint) / midpoint;
            Complex pointvector = new Complex(clickx * 2.0, clicky * 2.0);
            if (mouseButton == 8 || mouseButton == 9)
            {
                pointvector = Complex.Conjugate(displayWorker.averagePhase);
            }
            //Middle click
            if (mouseButton == 2 || mouseButton == 8 || mouseButton == 9)
            {
                double dbdiff = level1db - level2db;
                spin_gain.Value = spin_gain.Value + dbdiff;
            }
            //Rotate left click to 0 degrees
            double newPhase = spin_phase.Value - (pointvector.Phase * 360.0 / Math.Tau);

            if (newPhase < 0)
            {
                newPhase += 360.0;
            }
            if (newPhase > 360)
            {
                newPhase -= 360.0;
            }
            //Shift 180 for right click
            if (mouseButton == 3 || mouseButton == 8)
            {
                newPhase = newPhase + 180.0;
                if (newPhase > 360)
                {
                    newPhase -= 360.0;
                }
            }
            if (mouseButton == 1 || mouseButton == 3 || mouseButton == 8 || mouseButton == 9)
            {
                spin_phase.Value = newPhase;
            }
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            running = false;
            Application.Quit();
        }

        private void GainChanged(object sender, EventArgs a)
        {
            connection.Send((float)spin_gain.Value, (float)spin_phase.Value);
        }

        private void PhaseChanged(object sender, EventArgs a)
        {
            connection.Send((float)spin_gain.Value, (float)spin_phase.Value);
        }

        private void VFOChanged(object sender, EventArgs a)
        {
            connection.SetFrequency((float)spin_vfo.Value);
        }

        private void DrawMain(byte[] pixels, double level1, double level2, double combined)
        {
            Application.Invoke((o, e) =>
            {
                iqDisplay.Pixbuf = new Pixbuf(pixels, Gdk.Colorspace.Rgb, false, 8, DisplayWorker.WIDTH_HEIGHT, DisplayWorker.WIDTH_HEIGHT, DisplayWorker.WIDTH_HEIGHT * 3, null);
                level1db = 20 * Math.Log10(level1);
                level2db = 20 * Math.Log10(level2);
                double combineddb = 20 * Math.Log10(combined);
                if (level1db < -140)
                {
                    level1db = -140;
                }
                if (level2db < -140)
                {
                    level2db = -140;
                }
                if (combineddb < -140)
                {
                    combineddb = -140;
                }
                channel1Level.Text = "Channel 1: " + level1db.ToString("N1") + "db";
                channel2Level.Text = "Channel 2: " + level2db.ToString("N1") + "db";
                combinedLevel.Text = "Combined: " + combineddb.ToString("N1") + "db";
            });
        }
    }
}
