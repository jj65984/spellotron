using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Media;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Reflection;
using System.Threading;
using System.Windows.Media.Animation;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using Microsoft.Kinect;
using PoseDataCollection.PoseDataCollection;

namespace Spellotron
{
    /// <summary>
    /// Main "engine" of the program. The Controller in the MVC paradigm 
    /// </summary>
    public class Controller
    {
        //Fields
        bool debugStatus = false;       //Used to show/hide debug window
        bool kinectSensorStatus;        //Kinect sensor's connection status
        bool charIsReady = false;       //Has the current character been completely set up?
        bool isPaused = false;          //Is game paused?
        bool wordComplete = false;      //Has the current word been completed?
        bool kinectStatusIssues = false;//Is the Kinect having status issues? (ie disconnected)
        MainWindow gui;                 //The main full-screen window
        WordSummaryWindow win;          //Window containing game summary after word is completed
        DebugWindow debug;              //Secondary window containing debug info
        KinectInterface kinect;         //Interface between app and Kinect sensor
        VisualStreamProcessor visualSP; //Renders video feed, skeleton overlay
        PoseDetector poseDetector;      //Compares current frame with goal pose
        List<Pose> alphabetPoses;       //List of all Poses, A-Z
        List<string> wordList;          //List of all words
        Assembly assembly;              //Application's reflection, used to load pose and words libraries
        Game game;                      //Main game logic
        VisualBrush characterBrush;     //brush to paint current letter label
        DispatcherTimer timer;          //Main interal timer
        DateTime wordStartTime;         //Used to keep track of a word's total completion time
        DateTime charStartTime;         //Used to keep track of a character's total completion time
        TimeSpan charClock;             //Time span representing word's total completioin time
        TimeSpan wordClock;             //Time span representing character's total completion time
        int gameLevel = 0;              //Difficulty Level Number of the game

        //Constants
        static readonly int maxWordLen = 10;    //Maximum number of characters a word can contain
        static readonly int ticktime = 10;      //Timer tick interval in milliseconds        

        //Accesors for private fields
        public bool KinectSensorStatus { get { return kinectSensorStatus; } set { kinectSensorStatus = value; } }
        public bool CharIsReady { get { return charIsReady; } set { charIsReady = value; } }
        public bool IsPaused { get { return isPaused; } }
        public bool WordComplete { get { return wordComplete; } set { wordComplete = value; } }
        public bool KinectStatusIssues { get { return kinectStatusIssues; } }
        public string GameWord { get { return game.CurrentWord; } }
        public int GameScore { get { return game.Score; } }
        public TimeSpan GameTime { get { return wordClock; } }

        //Constructor
        public Controller(MainWindow mw, int level) 
        {
            assembly = Assembly.GetExecutingAssembly();
            gameLevel = level;
            gui = mw;
            debug = new DebugWindow(this);
            DebugWindowStatus(false);
            kinect = new KinectInterface(this);

            //If Kinect sensor properly initializes...
            if (kinectSensorStatus)
            {
                visualSP = new VisualStreamProcessor(this, kinect.GetSkeletonArrayLength());
                InitializePoses();
                InitializeWordList();
                InitializeComponents();
                game = new Game(this);
            }
                
            //If not report error and quit    
            else
            {
                gui.Error = true;
                MessageBox.Show("Kinect Sensor not detected or failed to initialize properly.\nMake sure it is firmly plugged in to a USB port and a power outlet.", "Kienct Sensor Required", MessageBoxButton.OK, MessageBoxImage.Error);
                
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Called when the Kinect has a new frame. This method is the "clock" of the game.
        /// It is used to create the video feed for player feed back, as well as detect the
        /// player's current position see if it matches with the onscreen letter
        /// </summary>
        /// <param name="s">Represents the palyer's skeleton during the current frame</param>
        /// <param name="c">The current color image frame from the Kinect's RGB camera</param>
        public void NewFrame(SkeletonFrame s, ColorImageFrame c)
        {
            visualSP.SkeletonFrame = s;
            visualSP.ColorFrame = c;
            visualSP.ProcessSkeleton();



            if ((!isPaused) && (!wordComplete))
            {
                Image i = visualSP.RenderColorAndSkeletonImage();
                gui.KinectVisualFeed = i;

                if (charIsReady)
                {
                    Pose currentPose = new Pose(i, visualSP.CurrentSkeleton);
                    double similarity = poseDetector.DetectPose(currentPose);
                    ColorCurrentCharacter(similarity);
                    SetBarDisplay(similarity);
                }
            }
        }

        /// <summary>
        /// Loads and initializes the pose alphabet from the embedded resource: 
        /// /resources/poses/poseLibrary.psx
        /// </summary>
        public void InitializePoses()
        {
            alphabetPoses = new List<Pose>(); //initalize field
            poseDetector = new PoseDetector(this);
            PoseCollection loadedPoses = new PoseCollection();
            IFormatter formatter = new BinaryFormatter();
            int poseCnt = 0;
            try
            {
                using (Stream poseStream = assembly.GetManifestResourceStream("Spellotron.resources.poses.alphabet.psx"))
                {
                    loadedPoses = (PoseCollection)formatter.Deserialize(poseStream);
                }
                alphabetPoses.Clear(); //just in case
                while (loadedPoses.HasNext())
                {
                    Pose p = loadedPoses.GetLast();
                    p.SkeletonImage = visualSP.RenderSkeletonImage(p.Skeleton);
                    alphabetPoses.Add(p);
                    poseCnt++;
                }
                WriteToDebugWindow("Initialization: " + alphabetPoses.Count + " alphabet poses sucessfully initialized");
            }
            catch (Exception e)
            {
                gui.Error = true;
                WriteToDebugWindow("Error loading internal poses: " + e.ToString());
                MessageBox.Show("There was an error loading required internal resources and the program must be shut down. The program's executable may be corrupted.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Loads and initializes the word list from the embedded resource: 
        /// /resources/poses/poseLibrary.psx
        /// </summary>
        public void InitializeWordList()
        {
            wordList = new List<string>();

            String listName = "Spellotron.resources.text.firstgrade.words";
            switch (gameLevel)
            {
                case 0:
                    listName = "Spellotron.resources.text.preK.words";
                    break;
                case 1:
                    listName = "Spellotron.resources.text.firstgrade.words";
                    break;
                case 2:
                    listName = "Spellotron.resources.text.secondgrade.words";
                    break;
                case 3:
                    listName = "Spellotron.resources.text.thirdgrade.words";
                    break;
                case 4:
                    listName = "Spellotron.resources.text.fourthgrade.words";
                    break;
                case 5:
                    listName = "Spellotron.resources.text.fifthgrade.words";
                    break;
                case 6:
                    listName = "Spellotron.resources.text.sixthgrade.words";
                    break;
                case 7:
                    listName = "Spellotron.resources.text.seventhgrade.words";
                    break;
                case 8:
                    listName = "Spellotron.resources.text.eighthgrade.words";
                    break;
                default:
                    listName = "Spellotron.resources.text.firstgrade.words";
                    break;
            }

            string word;
            try
            {
                using (Stream wordStream = assembly.GetManifestResourceStream(listName))
                {
                    wordList.Clear(); //also just in case
                    StreamReader wordStreamReader = new StreamReader(wordStream);
                    while ((word = wordStreamReader.ReadLine()) != null)
                    {
                        word = word.Trim();
                        wordList.Add(word);
                    }
                }
                WriteToDebugWindow("Initialization: " + wordList.Count + " words were sucessfully initialized");
            }
            catch (Exception e)
            {
                gui.Error = true;
                WriteToDebugWindow("Error loading internal wordlist: " + e.ToString());
                MessageBox.Show("There was an error loading required internal resources and the program must be shut down. The program's executable may be corrupted.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// Initializes various components (timer, brush)
        /// </summary>
        private void InitializeComponents()
        {
            //Set up internal timer
            timer = new DispatcherTimer(DispatcherPriority.Normal);
            timer.Interval = new TimeSpan(0, 0, 0, 0, ticktime);
            timer.Tick += new EventHandler(timer_Elapsed);
            timer.IsEnabled = true;

            //Set color gradient and brush for current letter
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0.5, 0.0);
            brush.EndPoint = new Point(0.5, 1.0);
            brush.GradientStops.Add(new GradientStop(Colors.Red, 0.0));
            brush.GradientStops.Add(new GradientStop(Colors.Yellow, 0.5));
            brush.GradientStops.Add(new GradientStop(Colors.Green, 1.0));
            Rectangle rec = new Rectangle();
            rec.Height = 101;
            rec.Width = 5;
            rec.Fill = brush;
            characterBrush = new VisualBrush();
            characterBrush.TileMode = TileMode.Tile;
            characterBrush.Visual = rec;
        }

        /// <summary>
        /// Finds and returns a random word from the word list that
        /// is less than 11 characters in length (the maximum displayable
        /// characters)
        /// </summary>
        /// <returns>A random word</returns>
        public string GetRandomWord()
        {
            String word = "";
            bool wordLenNotOK = true;
            var random = new Random();
            while (wordLenNotOK)
            {
                int wordIndex = random.Next(0, wordList.Count);
                word = wordList[wordIndex];
                if (word.Length <= maxWordLen)
                    wordLenNotOK = false;
            }

            WriteToDebugWindow("Random word selected: " + word);
            return word;
        }

        /// <summary>
        /// Takes a length of the current word in the Game class
        /// and creates a series of underscores - one for each letter
        /// in the word
        /// </summary>
        /// <param name="length">Length of current word</param>
        public void SetNextWord(string word)
        {            
            int len = word.Length;
            gui.WordSoFarLabel.Content = "";

            //Buid underscore label
            Label underscoreLabel = gui.UnderscoreLabel;
            string underscoreStr = "_ ";
            for (int i = 0; i < len; i++)
            {
                underscoreStr += "_ ";
            }
            underscoreStr.Trim();
            gui.UnderscoreLabel.Content = underscoreStr;
            gui.WordLabel.Content = word;

            wordStartTime = DateTime.Now;  //Start the timer
        }

        /// <summary>
        /// Updates the display with the new current character
        /// </summary>
        /// <param name="newChar">The new character</param>
        public void SetNextCharacter(char newChar)
        {
            charStartTime = DateTime.Now;
            Pose p = NewGoalPose(newChar);           
            SetPoseImage(p);
            poseDetector.GoalPose = p;
            gui.CurrentCharLabel.Content = newChar;
            if (gameLevel == 0)
                PlayCharacterAudio(newChar);

        }

        /// <summary>
        /// Plays the audio for the current character
        /// </summary>
        /// <param name="newChar">The new character</param>
        public void PlayCharacterAudio(char newChar)
        {
            //Play sound
            String soundName = "pack://application:,,,/Spellotron;component/resources/sounds/";
            soundName += newChar.ToString().ToLower();
            soundName += ".wav";
           
            Uri uri = new Uri(soundName);
            StreamResourceInfo sri = Application.GetResourceStream(uri);
            SoundPlayer letterComplete = new SoundPlayer(sri.Stream);
            letterComplete.Play();
        }

        /// <summary>
        /// Finds the new goal pose from the new character and sets it as the goal
        /// pose in the PoseDetector.
        /// </summary>
        /// <param name="newChar">The new character for which to find the pose</param>
        /// <returns></returns>
        public Pose NewGoalPose(char newChar)
        {
            string poseName = newChar.ToString();

            foreach (Pose p in alphabetPoses)
            {
                if (p.Name.Equals(poseName))
                    return p;
            }           
       
            //If we get here, then the goal pose can't be found. Fatal error.
            gui.Error = true;
            WriteToDebugWindow("Error loading new goal pose: " + poseName);
            MessageBox.Show("Cannot load vitial information. The executable may be corrupted.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
            return null;
        }

        /// <summary>
        /// Called by the Pose Detector when the user mimics the goal pose for the
        /// current character. Updates the wordSoFar in the bottom right of screen
        /// and gets the next character.
        /// </summary>
        public void CorrectPoseHit()
        {
            //Update word display
            string currentWord = game.CurrentWord;
            string wordSoFarStr = "";

            //Play sound
            Uri uri = new Uri(@"pack://application:,,,/Spellotron;component/resources/sounds/lettercomplete.wav");
            StreamResourceInfo sri = Application.GetResourceStream(uri);
            SoundPlayer letterComplete = new SoundPlayer(sri.Stream);
            letterComplete.Play();

            //build current word state
            for (int i = 0; i < currentWord.Length; i++)
            {
                if (i < game.CurrentCharIndex)
                    wordSoFarStr += game.CurrentLetters[i].ToString() + " ";
                else
                    wordSoFarStr += "  ";
            }

            gui.WordSoFarLabel.Content = wordSoFarStr;

            //Get next char
            game.UpdateScore(charClock);
            gui.ScoreLabel.Content = game.Score.ToString();
            game.NextCharacter();
        }

        /// <summary>
        /// Places the skeleton image of the goal pose to the main window to
        /// aid the user.
        /// </summary>
        /// <param name="p">Goal pose of the current character</param>
        public void SetPoseImage(Pose p)
        {
            Image i = new Image();
            i = visualSP.RenderSkeletonImage(p.Skeleton);
            gui.poseImage.Source = i.Source.Clone();
        }

        /// <summary>
        /// Called by Game obj when a word is completed. Hides some UI elements
        /// and opens a WordSummaryWindow to show results of the game.
        /// </summary>
        public void WordCompleted()
        {
            wordComplete = true;
            gui.CurrentCharLabel.Visibility = Visibility.Hidden;
            gui.poseImage.Visibility = Visibility.Hidden;
            gui.kinectVisualFeed.Visibility = Visibility.Hidden;
            gui.UnderscoreLabel.Visibility = Visibility.Hidden;
            gui.WordSoFarLabel.Visibility = Visibility.Hidden;
            BlurMainWindow(true);
            win = new WordSummaryWindow(this);
            win.Show();
        }

        /// <summary>
        /// Called when the WordSummaryWindow is closed - continues the game
        /// with a new word.
        /// </summary>
        public void StartNewWord()
        {
            win.Close();
            game.StartNewWord();            
            BlurMainWindow(false);
            gui.ScoreLabel.Content = 0;
            wordComplete = false;
            gui.UnderscoreLabel.Visibility = Visibility.Visible;
            gui.WordSoFarLabel.Visibility = Visibility.Visible;
            gui.CurrentCharLabel.Visibility = Visibility.Visible;
            gui.poseImage.Visibility = Visibility.Visible;
            gui.kinectVisualFeed.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Colors the current character label based on how close a user is
        /// to the goal pose of that character. Finds the color that correlates
        /// to the current frame's pose similarity to the goal pose on a 
        /// red-yellow-green gradient.
        /// </summary>
        /// <param name="similarity">Similarity percentage between current frame's pose and goal pose</param>
        public void ColorCurrentCharacter(double similarity)
        {
            //Adjust similarity
            if (similarity >= 91.0)
                similarity = 91.0;
            if (similarity <= 76.0)
                similarity = 76.0;
            similarity = similarity - 76.0;

            //Find and set the color that correlates to similarity
            double gradientLocation = 100.0 - (similarity * (100.0 / 15.0));

            if (gradientLocation < 1.0)
                gradientLocation = 1.1;
          
            characterBrush.Viewport = new Rect(0,  gradientLocation , 0.5, 101);
            gui.CurrentCharLabel.Foreground = characterBrush;
        }


        /// <summary>
        /// Set the bar that tells the user how close they are to the correct pose
        /// uses the same color as the character color
        /// 
        /// </summary>
        /// <param name="similarity">Similarity percentage between current frame's pose and goal pose</param>
        public void SetBarDisplay(double similarity)
        {
            gui.barDisplay.Width = (similarity / 100) * gui.barOutline.Width;
            //Adjust similarity
            if (similarity >= 91.0)
                similarity = 91.0;
            if (similarity <= 76.0)
                similarity = 76.0;
            similarity = similarity - 76.0;

            //Find and set the color that correlates to similarity
            double gradientLocation = 100.0 - (similarity * (100.0 / 15.0));

            if (gradientLocation < 1.0)
                gradientLocation = 1.1;

            characterBrush.Viewport = new Rect(0, gradientLocation, 0.5, 101);
            gui.barDisplay.Fill = characterBrush;
        }

        /// <summary>
        /// Blurs and un-blurs the main game window. Used after a word is completed
        /// to shift user focus away from game window to the post word report
        /// </summary>
        /// <param name="blurWindow">True to blur window, false to un-blur window</param>
        public void BlurMainWindow(bool blurWindow)
        {
            BlurEffect e = new BlurEffect();
            e.Radius = 0;
            if (blurWindow)
                e.Radius = 20;
            gui.grid.Effect = e;
        }

        /// <summary>
        /// Increases the letter and word timers, then updates the on-screen timer labels
        /// </summary>
        public void UpdateTimerLabel()
        {
            charClock = DateTime.Now - charStartTime;
            wordClock = DateTime.Now - wordStartTime;
            gui.CharTimeLabel.Content = string.Format("        {0:D2}:{1:D2}.{2:D2}", charClock.Minutes, charClock.Seconds, charClock.Milliseconds / 10);
            gui.WordTimeLabel.Content = string.Format("           {0:D2}:{1:D2}.{2:D2}", wordClock.Minutes, wordClock.Seconds, wordClock.Milliseconds / 10);
        }

        /// <summary>
        /// Changes the Kinect sensor's view angle
        /// </summary>
        /// <param name="angleChange">The number of degrees to change the angle</param>
        public void ChangeKinectViewAngle(int angleChange)
        {
            if(kinectSensorStatus) //make sure Kinect is connected
                 kinect.ChangeViewAngle(angleChange);
        }

        /// <summary>
        /// Converts a 3D skeleton point to a 2D Point for use in 
        /// drawing bones on the video feed.
        /// </summary>
        /// <param name="skelP">A point on a skeleton</param>
        /// <returns>That point's position on a 2D image</returns>
        public Point MapSkeletonPoint(SkeletonPoint skelP)
        {
            return kinect.SkeletonPointToColorPoint(skelP);
        }

        /// <summary>
        /// Handles the pausing of the game by pausing timers, removing
        /// the goal character from the screen and placing an animated
        /// "Pause" label in its place. Also used to unpause the game and
        /// start the times and return the current char back to screen.
        /// </summary>
        /// <param name="status"></param>
        public void Pause(bool status)
        {
            isPaused = status;

            //Pause animation - fade out
            DoubleAnimation fade = new DoubleAnimation();
            fade.From = 1.0;
            fade.To = 0.0;
            fade.BeginTime = TimeSpan.FromMilliseconds(0);
            fade.Duration = new Duration(TimeSpan.FromMilliseconds(1500));

            //Pause animation - fade in
            DoubleAnimation ret = new DoubleAnimation();
            ret.From = 0.0;
            ret.To = 1.0;
            ret.BeginTime = TimeSpan.FromMilliseconds(1500);
            ret.Duration = new Duration(TimeSpan.FromMilliseconds(1000));

            //storyboard for animation
            Storyboard sb = new Storyboard();
            sb.Children.Add(fade);
            sb.Children.Add(ret);
            sb.RepeatBehavior = RepeatBehavior.Forever;
            gui.RegisterName("pause", gui.PauseLabel);
            Storyboard.SetTargetName(fade, "pause");
            Storyboard.SetTargetName(ret, "pause");
            Storyboard.SetTargetProperty(fade, new PropertyPath(Label.OpacityProperty));
            Storyboard.SetTargetProperty(ret, new PropertyPath(Label.OpacityProperty));

            //If game is paused
            if (isPaused)
            {
                //Hide some UI elements
                gui.CurrentCharLabel.Visibility = Visibility.Hidden;
                gui.poseImage.Visibility = Visibility.Hidden;
                gui.kinectVisualFeed.Visibility = Visibility.Hidden;
                gui.UnderscoreLabel.Visibility = Visibility.Hidden;
                gui.WordSoFarLabel.Visibility = Visibility.Hidden;
                gui.PauseLabel.Visibility = Visibility.Visible;
                sb.Begin(gui); //Run animation
            }

            //If game is unpaused
            else
            {
                //Show Hidden UI elements
                gui.PauseLabel.Visibility = Visibility.Hidden;
                gui.CurrentCharLabel.Visibility = Visibility.Visible;
                gui.poseImage.Visibility = Visibility.Visible;
                gui.kinectVisualFeed.Visibility = Visibility.Visible;
                gui.UnderscoreLabel.Visibility = Visibility.Visible;
                gui.WordSoFarLabel.Visibility = Visibility.Visible;
                sb.Stop(gui);  //Stop animation
                gui.UnregisterName("pause");

                //Update timers to account for time paused
                charStartTime = DateTime.Now - charClock;
                wordStartTime = DateTime.Now - wordClock;
            }
        }

        /// <summary>
        /// Called when the Kinect sensor experiences a status chage (it is unplugged,
        /// re-connected, etc). Used to handle these changes across the program and provide
        /// feedback of the situation to the user
        /// </summary>
        /// <param name="status">The Kinect sensor's new status</param>
        public void KinectStatusChange(KinectStatus status)
        {
            //Set up animation for main window status label
            DoubleAnimation fade = new DoubleAnimation();
            fade.From = 1.0;
            fade.To = 0.2;
            fade.BeginTime = TimeSpan.FromMilliseconds(0);
            fade.Duration = new Duration(TimeSpan.FromMilliseconds(700));
            DoubleAnimation ret = new DoubleAnimation();
            ret.From = 0.2;
            ret.To = 1.0;
            ret.BeginTime = TimeSpan.FromMilliseconds(00);
            ret.Duration = new Duration(TimeSpan.FromMilliseconds(300));
            Storyboard sb = new Storyboard();
            sb.Children.Add(fade);
            sb.Children.Add(ret);
            sb.RepeatBehavior = RepeatBehavior.Forever;
            gui.RegisterName("status", gui.KinectStatusLabel);
            Storyboard.SetTargetName(fade, "status");
            Storyboard.SetTargetName(ret, "status");
            Storyboard.SetTargetProperty(fade, new PropertyPath(Label.OpacityProperty));
            Storyboard.SetTargetProperty(ret, new PropertyPath(Label.OpacityProperty));

            //If the Kinect has been disconnected
            if (status == KinectStatus.Disconnected)
            {
                WriteToDebugWindow("Kinect: sensor disconnected");
                isPaused = true;
                kinectStatusIssues = true;
                BitmapImage kinectRequiredImg = new BitmapImage(new Uri(@"pack://application:,,,/Spellotron;component/resources/images/requires_kinect.png"));
                gui.kinectVisualFeed.Source = kinectRequiredImg;
                gui.kinectVisualFeed.Visibility = Visibility.Visible;
                gui.PauseLabel.Visibility = Visibility.Hidden;
                gui.CurrentCharLabel.Visibility = Visibility.Hidden;
                gui.poseImage.Visibility = Visibility.Hidden;
                gui.UnderscoreLabel.Visibility = Visibility.Hidden;
                gui.WordSoFarLabel.Visibility = Visibility.Hidden;
                gui.KinectStatusLabel.Content = "Kinect sensor disconnected";
                gui.KinectStatusLabel.Visibility = Visibility.Visible;
                sb.Begin(gui);               
            }

            //If the Kinect is initializing
            if (status == KinectStatus.Initializing)
            {                
                WriteToDebugWindow("Kinect: sensor initializing");
                gui.KinectStatusLabel.Content = "Kinect initializing...";
            }

            //If the Kinect is connected
            if (status == KinectStatus.Connected)
            {
                WriteToDebugWindow("Kinect: sensor connected");                
                kinect.InitializeKinectServices();
                kinectStatusIssues = false;
                gui.PauseLabel.Visibility = Visibility.Hidden;
                gui.CurrentCharLabel.Visibility = Visibility.Visible;
                gui.poseImage.Visibility = Visibility.Visible;
                gui.WordSoFarLabel.Visibility = Visibility.Visible;
                gui.UnderscoreLabel.Visibility = Visibility.Visible;
                gui.KinectStatusLabel.Visibility = Visibility.Hidden;
                gui.kinectVisualFeed.Visibility = Visibility.Visible;
                sb.Stop(gui);
                gui.UnregisterName("status");
                
                //Update timers to account for time paused
                isPaused = false;
                charStartTime = DateTime.Now - charClock;
                wordStartTime = DateTime.Now - wordClock;
            }
        }

        /// <summary>
        /// Shows or hides the debug window
        /// </summary>
        /// <param name="status">new debug window visual status</param>
        public void DebugWindowStatus(bool status)
        {
            if (status)
            {
                debug.Show();
                debug.Topmost = true;
            }
            else
            {
                debug.Hide();
            }

            debugStatus = status;
        }

        /// <summary>
        /// Write a timestamped message on the debug window (if active)
        /// </summary>
        /// <param name="s">Message to write to debug window</param>
        public void WriteToDebugWindow(string s)
        {
            try
            {
                DateTime cur = DateTime.Now;
                String time = String.Format("[{0:HH:mm:ss.fff}]", cur);
                debug.log.AppendText(time + " " + s + "\n");
                debug.log.ScrollToLine(debug.log.LineCount - 1);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Event hander for internal timer. Tick frequency is set by
        /// static int ticktime
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void timer_Elapsed(object sender, EventArgs e)
        {
            if (charIsReady && !isPaused && !wordComplete)
                UpdateTimerLabel();
        }
    }
}
