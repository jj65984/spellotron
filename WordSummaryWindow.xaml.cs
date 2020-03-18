using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Resources;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Spellotron
{
    /// <summary>
    /// This window pops up when a user has completed it word. It shows a summary
    /// including the word they completed, the time it took to complete it, as well
    /// as their final score. When this window closes the game automatically continues
    /// with a new word.
    /// </summary>
    public partial class WordSummaryWindow : Window
    {
        //Fields
        Controller ctrl;

        //Constructor
        public WordSummaryWindow(Controller c)
        {
            InitializeComponent();
            ctrl = c;

            //**** Animate Word Complete label ****
            LinearGradientBrush gradientBrush = new LinearGradientBrush(); 
            GradientStop midGrad = new GradientStop(Colors.DarkGray, 0.0); 
            gradientBrush.GradientStops.Add(new GradientStop(Colors.Gold, 0.0));
            gradientBrush.GradientStops.Add(midGrad);
            gradientBrush.GradientStops.Add(new GradientStop(Colors.Gold, 1.0));
            wordCompleteLabel.Foreground = gradientBrush;

            DoubleAnimation offsetDA = new DoubleAnimation();
            offsetDA.From = 0.0;
            offsetDA.To = 1.0;
            offsetDA.Duration = new Duration(TimeSpan.FromMilliseconds(1500));
            offsetDA.AutoReverse = true;

            this.RegisterName("MiddleGradient", midGrad); 
            Storyboard.SetTargetName(offsetDA, "MiddleGradient");
            Storyboard.SetTargetProperty(offsetDA, new PropertyPath(GradientStop.OffsetProperty));           
            
            Storyboard gradientStopAnimationStoryboard = new Storyboard();             
            gradientStopAnimationStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            gradientStopAnimationStoryboard.Children.Add(offsetDA);
            gradientStopAnimationStoryboard.Begin(this);

            //**** Set results labels ****
            Run wRun = new Run("   " + ctrl.GameWord.ToLower());
            wRun.Foreground = new SolidColorBrush(Colors.White);
            wLabel.Inlines.Add(wRun);
            
            TimeSpan gametime = ctrl.GameTime;
            Run tRun = new Run(String.Format("    {0:D2}:{1:D2}.{2:D2}", gametime.Minutes, gametime.Seconds, gametime.Milliseconds / 10));
            tRun.Foreground = new SolidColorBrush(Colors.White);
            tLabel.Inlines.Add(tRun);
            
            Run sRun = new Run("  " + ctrl.GameScore.ToString());
            sRun.Foreground = new SolidColorBrush(Colors.White);
            sLabel.Inlines.Add(sRun);

            //**** Animate continue string label ****
            DoubleAnimation fade = new DoubleAnimation();
            fade.From = 1.0;
            fade.To = 0.3;
            fade.BeginTime = TimeSpan.FromMilliseconds(0);
            fade.Duration = new Duration(TimeSpan.FromMilliseconds(500));

            DoubleAnimation ret = new DoubleAnimation();
            ret.From = 0.3;
            ret.To = 1.0;
            ret.BeginTime = TimeSpan.FromMilliseconds(500);
            ret.Duration = new Duration(TimeSpan.FromMilliseconds(500));

            Storyboard continueLabelStoryboard = new Storyboard();
            continueLabelStoryboard.Children.Add(fade);
            continueLabelStoryboard.Children.Add(ret);
            continueLabelStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            this.RegisterName("ContinueLabel", this.continueLabel);
            Storyboard.SetTargetName(fade, "ContinueLabel");
            Storyboard.SetTargetName(ret, "ContinueLabel");
            Storyboard.SetTargetProperty(fade, new PropertyPath(TextBlock.OpacityProperty));
            Storyboard.SetTargetProperty(ret, new PropertyPath(TextBlock.OpacityProperty));
            continueLabelStoryboard.Begin(this);

            //Play sound
            Uri uri = new Uri(@"pack://application:,,,/Spellotron;component/resources/sounds/wordcomplete.wav");
            StreamResourceInfo sri = Application.GetResourceStream(uri);
            SoundPlayer wordComplete = new SoundPlayer(sri.Stream);
            wordComplete.Play();
        }

        /// <summary>
        /// Handler for when a key is pressed inside the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Escape) || (e.Key == Key.Enter))
                if(!ctrl.KinectStatusIssues)
                     ctrl.StartNewWord();  //Continue          
        }
    }
}
