using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace Spellotron
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Fields
        Controller ctrl;          //The app's main controller
        bool error = false;       //Used to determine if app exit is error or user initiated
        Label underscoreLabel;    //Displays underscores for goal word
        Label wordLabel;          //Displays the goal word
        Label wordSoFarLabel;     //Displays characters completed
        Label charTimeLabel;      //Displays time spent completing character
        Label wordTimeLabel;      //Displays time spent completing word
        Label pauseLabel;         //Displays "Paused" on screen when app is paused
        Label kinectStatusLabel;
        double MaxVisualHeight;
        double MaxVisualWidth;

        //Accessors for private fields
        public Image KinectVisualFeed { set { kinectVisualFeed.Source = value.Source; } }
        public bool Error { set { error = value; } }
        public Label UnderscoreLabel { get { return underscoreLabel; } }
        public Label CurrentCharLabel { get { return currentCharLabel; } }
        public Label WordLabel { get { return wordLabel; } }
        public Label WordSoFarLabel { get { return wordSoFarLabel; } }
        public Label WordTimeLabel { get { return wordTimeLabel; } }
        public Label CharTimeLabel { get { return charTimeLabel; } }
        public Label PauseLabel { get { return pauseLabel; } }
        public Label ScoreLabel { get { return scoreLabel; } }
        public Label KinectStatusLabel { get { return kinectStatusLabel; } }

        //Constructor
        public MainWindow()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(ResolvePoseAssembly); //Load PoseDataCollection.dll     
            InitializeComponent();
            InitializeVisualComponents();
            try
            {
                KinectSensor kinectCheck = KinectSensor.KinectSensors.FirstOrDefault();
                LevelSelect ls = new LevelSelect();
                ctrl = new Controller(this, ls.getLevel());
                ls.ShowDialog();

                if (ls.getLevel() == -1)
                {
                    error = true;
                    this.Close();
                    return;
                    //Application.Current.Shutdown();
                }
            }
            catch (TypeInitializationException e)
            {
                error = true;
                MessageBox.Show("Kinect Sensor not detected or failed to initialize properly.\nMake sure it is firmly plugged in to a USB port and a power outlet.", "Kienct Sensor Required", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        /// <summary>
        /// Initializes the visual components of the main screen
        /// </summary>
        private void InitializeVisualComponents()
        {
            //Adjust screen for 4x3 ratio
            MaxVisualHeight = SystemParameters.PrimaryScreenHeight;
            MaxVisualWidth = (MaxVisualHeight / 3) * 4;
            grid.MaxHeight = MaxVisualHeight;
            grid.MaxWidth = MaxVisualWidth;

            underscoreLabel = new Label();
            underscoreLabel.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./resources/fonts/#Cousine");
            underscoreLabel.Foreground = new SolidColorBrush(Colors.White);
            underscoreLabel.FontSize = 100;
            underscoreLabel.VerticalAlignment = VerticalAlignment.Bottom;
            underscoreLabel.HorizontalAlignment = HorizontalAlignment.Right;

            wordLabel = new Label();
            wordLabel.Foreground = new SolidColorBrush(Colors.White);
            wordLabel.FontSize = 75;
            wordLabel.VerticalAlignment = VerticalAlignment.Top;
            wordLabel.HorizontalAlignment = HorizontalAlignment.Right;

            wordSoFarLabel = new Label();
            wordSoFarLabel.Foreground = new SolidColorBrush(Colors.White);
            wordSoFarLabel.FontSize = 100;
            wordSoFarLabel.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./resources/fonts/#Cousine");
            wordSoFarLabel.VerticalAlignment = VerticalAlignment.Bottom;
            wordSoFarLabel.HorizontalAlignment = HorizontalAlignment.Right;

            wordTimeLabel = new Label();
            wordTimeLabel.Foreground = new SolidColorBrush(Colors.White);
            wordTimeLabel.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./resources/fonts/#Let's Go Digital");
            wordTimeLabel.FontSize = 50;
            wordTimeLabel.Margin = new Thickness(0, 55, 0, 0);

            charTimeLabel = new Label();
            charTimeLabel.Foreground = new SolidColorBrush(Colors.White);
            charTimeLabel.FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./resources/fonts/#Let's Go Digital");
            charTimeLabel.FontSize = 70;
            charTimeLabel.HorizontalAlignment = HorizontalAlignment.Left;
            charTimeLabel.VerticalAlignment = VerticalAlignment.Top;

            pauseLabel = new Label();
            pauseLabel.Visibility = Visibility.Hidden;
            pauseLabel.Foreground = new SolidColorBrush(Colors.White);
            pauseLabel.FontFamily = new FontFamily("Arial Black");
            pauseLabel.Content = "Paused";
            pauseLabel.FontSize = 90;
            pauseLabel.HorizontalAlignment = HorizontalAlignment.Center;
            pauseLabel.VerticalAlignment = VerticalAlignment.Center;

            kinectStatusLabel = new Label();
            kinectStatusLabel.Visibility = Visibility.Hidden;
            kinectStatusLabel.Foreground = new SolidColorBrush(Colors.White);
            kinectStatusLabel.FontFamily = new FontFamily("Arial Black");
            kinectStatusLabel.FontSize = 60;
            kinectStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            kinectStatusLabel.VerticalAlignment = VerticalAlignment.Center;

            grid.Children.Add(underscoreLabel);
            grid.Children.Add(wordSoFarLabel);
            grid.Children.Add(wordLabel);
            grid.Children.Add(charTimeLabel);
            grid.Children.Add(wordTimeLabel);
            grid.Children.Add(pauseLabel);
            grid.Children.Add(kinectStatusLabel);
        }

        /// <summary>
        /// Handler for when a key is pressed down inside the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //Escpae
            if (e.Key == Key.Escape)
                this.Close();

            //Pause
            if ((e.Key == Key.Pause) || (e.Key == Key.P))
                if(!ctrl.KinectStatusIssues)
                     ctrl.Pause(!ctrl.IsPaused);

            if (e.Key == Key.Space)
                ctrl.CorrectPoseHit();
        }

        /// <summary>
        /// Called right before the app is shut down in order to ask if that's what the user
        /// really wants.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!error)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you would like to quit?", "Quit?", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    e.Cancel = true;
            }
        }

        /// <summary>
        /// Shutdowns the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Main window context menu handler for Exit option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CM_Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Main window context menu handler for Show Debug Window option
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CM_ShowDebug(object sender, RoutedEventArgs e)
        {
            MenuItem m = sender as MenuItem;
            ctrl.DebugWindowStatus(m.IsChecked);
        }

        /// <summary>
        /// Main window context menu for online help. Opens the program's website
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void CM_Help(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://student.cs.appstate.edu/jj65984/spellotron");
        }

        /// <summary>
        /// Main window context menu handler for showing the About pop-up window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CM_About(object sender, RoutedEventArgs e)
        {
            new AboutWindow().Show();
        }

        /// <summary>
        /// Use to initalize the PoseDataCollection assembly on startup by accessing it as an embedded resource
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns>The program's assembly with the PoseDataCollection added</returns>
        static Assembly ResolvePoseAssembly(object sender, ResolveEventArgs args)
        {
            Assembly parentAssembly = Assembly.GetExecutingAssembly();
            using (Stream stream = parentAssembly.GetManifestResourceStream("Spellotron.resources.dll.PoseDataCollection.dll"))
            {
                byte[] block = new byte[stream.Length];
                stream.Read(block, 0, block.Length);
                return Assembly.Load(block);
            }
        }

        private void kinectVisualFeed_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }
    }

    /// <summary>
    /// About pop-up window
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow() {

            this.Title = "About Spellotron";        
            this.Width = 350;
            this.Height = 140;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ShowInTaskbar = false;
            this.WindowStyle = WindowStyle.ToolWindow;
            
            Grid grid = new Grid();
            this.AddChild(grid);

            TextBlock text = new TextBlock();
            text.Inlines.Add(new Run("                Spellotron 1.0\n" +
                                     "© 2012 Appalachian State University \n\n" +
                                     "Designed by: Dr. Rahman Tashakkori\n" +
                                     "                      Jack Jordan\n" +
                                     "                      Ahmad Ghadiri\n" +
                                     "Developed by: Jack Jordan"));
            text.HorizontalAlignment = HorizontalAlignment.Right;            

            Image spLogo = new Image();
            spLogo.Source = new BitmapImage(new Uri(@"pack://application:,,,/Spellotron;component/resources/images/splogo.png"));
            spLogo.HorizontalAlignment = HorizontalAlignment.Left;

            grid.Children.Add(spLogo);
            grid.Children.Add(text);
        }        
    }
}
