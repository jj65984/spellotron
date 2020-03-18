using System;
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

namespace Spellotron
{
    /// <summary>
    /// Interaction logic for LevelSelect.xaml
    /// </summary>
    public partial class LevelSelect : Window
    {
        private int level = -1;
        public LevelSelect()
        {
            InitializeComponent();
            textBlock2.Text = "                   Spellotron 1.01\n" +
                                     "© 2014 ASU Dept. Comuter Science \n\n" +
                                     "Designed by: Dr. Rahman Tashakkori\n" +
                                     "                      Jack Jordan\n" +
                                     "                      Ahmad Ghadiri\n" +
                                     "Developed by: Jack Jordan\n" +
                                     "                        Clint Guin \n";
        }

        public int getLevel()
        {
            return level;
        }

        private void button0_Click(object sender, RoutedEventArgs e)
        {
            level = 0;
            this.Close();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            level = 1;
            this.Close();
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            level = 2;
            this.Close();
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            level = 3;
            this.Close();
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            level = 4;
            this.Close();
        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {
            level = 5;
            this.Close();
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            level = 6;
            this.Close();
        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            level = 7;
            this.Close();
        }

        private void button8_Click(object sender, RoutedEventArgs e)
        {
            level = 8;
            this.Close();
        }




    }
}
