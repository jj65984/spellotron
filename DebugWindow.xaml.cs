using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Spellotron
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {
        //Fields
        private Controller ctrl; //The application's controller

        //Construcor
        public DebugWindow(Controller c)
        {
            InitializeComponent();
            ctrl = c;            
        }

        /// <summary>
        ///  Debug window context menu handler - writes the current contents of 
        ///  the Debug Window to a timestamped file at ./debug log/[filename]
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CM_SaveLog(object sender, RoutedEventArgs e)
        {
            try
            {
                //Create new folder for logs if doesn't exist
                String newDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "debug logs");
                Directory.CreateDirectory(newDir);

                //Create file named after current date  + time
                String filename = String.Format("{0:ddMMMyyyy-HH.mm.ss}.log", DateTime.Now);
                String path = newDir + @"\" + filename;

                //Get text from log window
                String[] lines = log.Text.Split('\n');
                using (StreamWriter f = new StreamWriter(path, true))
                {
                    //Write each line to file
                    foreach (String s in lines)
                    {
                        f.WriteLine(s);
                    }
                }
                ctrl.WriteToDebugWindow("Saved log to file " + path);
            }
            catch
            {
                ctrl.WriteToDebugWindow("Error saving log: " + e.ToString());
            }
        }

        /// <summary>
        /// Debug window context menu handler - increases the Kinect sensor's viewing angle
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CM_KinectUp(object sender, RoutedEventArgs e)
        {
            ctrl.ChangeKinectViewAngle(3);
        }

        /// <summary>
        ///  Debug window context menu handler - decreases the Kinect sensor's viewing angle
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CM_KinectDown(object sender, RoutedEventArgs e)
        {
            ctrl.ChangeKinectViewAngle(-3);
        }

        /// <summary>
        /// Handler for when the window is closing. Cancels the window from closing and 
        /// hides it instead.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClosingWin(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }       
    }
}
