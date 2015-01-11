using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Video2PSD
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HQDShowPlayer player;

        private Nullable<Int64> MarkIn = null;
        private Nullable<Int64> MarkOut = null;

        private DispatcherTimer SeekBarTimer = new DispatcherTimer();

        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }

            player = new HQDShowPlayer(VideoPanel);
            SeekBarTimer.Tick += SeekBarTimer_Tick;
            SeekBarTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
        }

        private void OpenVideoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                player.OpenFile(ofd.FileName);
                SeekBarTimer.Start();
                player.Play();

                //Image change
                Image buttonSprite = (PlayPauseButton.Content as Image);
                buttonSprite.Width = 9;
                buttonSprite.Height = 9;
                buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(11, 1, 9, 9));
            }
        }

        private void MainWindow1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SeekBarTimer.Stop();
            player.Dispose();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (player.GetState() == DirectShowLib.FilterState.Running) //Playing, do pause
            {
                player.Pause();

                //Image change
                Image buttonSprite = (PlayPauseButton.Content as Image);
                buttonSprite.Width = 11;
                buttonSprite.Height = 11;
                buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(0, 0, 11, 11));
            }
            else //Paused or stopped, do play
            {
                player.Play();

                //Image change
                Image buttonSprite = (PlayPauseButton.Content as Image);
                buttonSprite.Width = 9;
                buttonSprite.Height = 9;
                buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(11, 1, 9, 9));
            }
        }

        private void BeginButton_Click(object sender, RoutedEventArgs e)
        {
            player.SeekAbsolute(0);
        }

        private void StepBackButton_Click(object sender, RoutedEventArgs e)
        {
            player.StepBack();
        }

        private void StepForwardButton_Click(object sender, RoutedEventArgs e)
        {
            player.Step();
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            player.SeekAbsolute(-1);
        }

        private void MarkInButton_Click(object sender, RoutedEventArgs e)
        {
            MarkIn = player.GetCurrentPos();
            SeekBar.Ticks[0] = (double)MarkIn / (double)player.GetEndPosition();
        }

        private void MarkOutButton_Click(object sender, RoutedEventArgs e)
        {
            MarkOut = player.GetCurrentPos();
            SeekBar.Ticks[1] = (double)MarkOut / (double)player.GetEndPosition();
        }

        private void SeekBarTimer_Tick(object sender, EventArgs e)
        {
            Int64 currPos = player.GetCurrentPos();
            Int64 endPos = player.GetEndPosition();

            double scalarDone = (double)currPos / (double)endPos;

            SeekBar.Value = scalarDone;
        }

        private void SeekBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point downAt = e.GetPosition(SeekBar as IInputElement);
            Debug.WriteLine("Mouse Down at: {0}", downAt);

            e.Handled = true;
        }
    }
}
