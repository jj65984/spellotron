using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Kinect;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using System.Threading.Tasks;
using System.Timers;

namespace Spellotron
{
    /// <summary>
    /// Controls the program's interatction with and data from the Kinect sensor.
    /// </summary>
    class KinectInterface
    {
        Controller ctrl;
        SkeletonFrame skeletonFrame; //Current frame of kinect's tracked players
        KinectSensor sensor;         //Refernce to the kinect
        ColorImageFrame colorFrame;  //Current frame from kienct's color camera
        SpeechRecognitionEngine sre; //The speech recognition engine

        /// <summary>
        /// Constructor. Initializes the Kinect Sensor and links it to the app.
        /// </summary>
        /// <param name="c">The program's controller</param>
        public KinectInterface(Controller c)
        {
            ctrl = c;
            KinectSensor.KinectSensors.StatusChanged += new EventHandler<StatusChangedEventArgs>(SensorStatusChange);
            ctrl.KinectSensorStatus = InitializeKinectServices();
            if(ctrl.KinectSensorStatus)
                ctrl.WriteToDebugWindow("Kinect: Initial initialization successful");
        }

        /// <summary>
        /// Initializes the Kinect sensor upon program startup or when
        /// the user is manually connects a Kinect
        /// </summary>
        /// <returns>If initialization succeeded</returns>
        public bool InitializeKinectServices()
        {
            //Try and grab first Kinect connected to system
            sensor = KinectSensor.KinectSensors.FirstOrDefault();
            if (sensor == null || sensor.Status != KinectStatus.Connected)
            {
                ctrl.WriteToDebugWindow("Kinect: Sensor initialization failed");
                return false;
            }

            //Smooths skeleton jitter
            var parameters = new TransformSmoothParameters
            {
                Smoothing = 0.99f,
                Correction = 0.05f,
                Prediction = 0.5f,
                JitterRadius = 0.05f,
                MaxDeviationRadius = 0.05f
            };

            //Open streams and start the sensor
            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            sensor.SkeletonStream.Enable(parameters);
            sensor.Start();

            //Add event handler
            sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(AllFramesReady);

            //Initialize speech recognition
            if (!InitializeSpeechRec())
                return false;

            //If down here, then everything's working correctly.            
            return true;
        }

        /// <summary>
        /// Initializes the speech recognition component of Kinect. Uses Microsoft's
        /// speech recognition library.
        /// </summary>
        /// <returns>If initialization was successful</returns>
        public bool InitializeSpeechRec()
        {
            //Begin initialization
            RecognizerInfo ri = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault();
            if (ri == null)
            {
                ctrl.WriteToDebugWindow("Kinect: Speech recognition initialization failed");
                return false;
            }

            //Add the words we want to recognize
            sre = new SpeechRecognitionEngine(ri.Id);
            Choices options = new Choices();
            options.Add("continue");
            options.Add("pause");
            options.Add("up");
            options.Add("down");

            //Build a grammer from the above words
            GrammarBuilder gb = new GrammarBuilder { Culture = ri.Culture };
            gb.Append(options);
            Grammar g = new Grammar(gb);
            sre.LoadGrammar(g);
            sre.SpeechRecognized += SpeechRecognized;

            //Start the internal audio stream from the Kinect
            System.IO.Stream s = sensor.AudioSource.Start();
            sre.SetInputToAudioStream(s, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            sre.RecognizeAsync(RecognizeMode.Multiple);
            return true;
        }

        /// <summary>
        /// Event handler for when all frames are ready on the Kinect side and
        /// are sent over. Simply takes the updated frames and sends them to
        /// the controller which then sends them to the appropriate stream processor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (skeletonFrame = e.OpenSkeletonFrame())
            {
                using (colorFrame = e.OpenColorImageFrame())
                {                    
                    ctrl.NewFrame(skeletonFrame, colorFrame);
                }
            }

        }

        /// <summary>
        /// Event listener that is called when one of the key words is picked
        /// up by the Kinect. Examine the word and if the confidence level is
        /// high enough, enact the spoken command.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //Make sure we're 90% confident in the command
            if (e.Result.Confidence < 0.9)
                return;

            switch (e.Result.Text)
            {               
                case "pause":
                    if(!ctrl.KinectStatusIssues)
                        ctrl.Pause(!ctrl.IsPaused);                 
                    break;
                case "continue":
                    if (ctrl.WordComplete && (!ctrl.KinectStatusIssues))
                        ctrl.StartNewWord();
                    break;
                case "up":
                    ChangeViewAngle(2);
                    break;
                case "down":
                    ChangeViewAngle(-2);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Event listener that is called when the Kinect sensor's status is change
        /// (ie it is unplugged)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SensorStatusChange(object sender, StatusChangedEventArgs e)
        { 
            ctrl.KinectStatusChange(e.Status);   
        }

        /// <summary>
        /// Spans a thread to change the Kinect sensor's elevation angle
        /// </summary>
        /// <param name="newAngle">new elevation angle</param>
        public void ChangeViewAngle(int change)
        {
            int newAngle = sensor.ElevationAngle + change;
            Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (sensor != null && sensor.Status == KinectStatus.Connected)
                            sensor.ElevationAngle = newAngle;
                    }
                    catch (InvalidOperationException)
                    {
                        MessageBox.Show("The elevation angle can only be changed 15 times every 20 seconds.\nPlease allow 20 seconds for motor cool down.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        //Do nothing
                    }
                });
        }

        /// <summary>
        /// The max number of skeletons in the Skeleton Stream's list. Used in array creation.
        /// </summary>
        /// <returns>The length of the Skeleton Stream's array of Skeleton objs</returns>
        public int GetSkeletonArrayLength()
        {
            return sensor.SkeletonStream.FrameSkeletonArrayLength;          
        }

        /// <summary>
        /// Translates the 3D point of a skeleton joint position into a 2D point on the ColorStream
        /// Used for easy mapping of skeleton points onto the color image.
        /// </summary>
        /// <param name="skelP">2D Point representing the SkeletonPoint's position on the ColorStream</param>
        /// <returns></returns>
        public Point SkeletonPointToColorPoint(SkeletonPoint skelP)
        {
            ColorImagePoint colP = sensor.MapSkeletonPointToColor(skelP, sensor.ColorStream.Format);
            return new Point((double)colP.X, (double)colP.Y);
        }
    }
}
