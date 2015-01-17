using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
                Title = "Video2PSD - " + ofd.SafeFileName;

                player.OpenFile(ofd.FileName);
                SeekBarTimer.Start();
                DoPlay();
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
                DoPause();
            }
            else //Paused or stopped, do play
            {
                DoPlay();
            }
        }

        private void DoPause()
        {
            player.Pause();

            //Image change
            Image buttonSprite = (PlayPauseButton.Content as Image);
            buttonSprite.Width = 11;
            buttonSprite.Height = 11;
            buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(0, 0, 11, 11));
        }

        private void DoPlay()
        {
            player.Play();

            //Image change
            Image buttonSprite = (PlayPauseButton.Content as Image);
            buttonSprite.Width = 9;
            buttonSprite.Height = 9;
            buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(11, 1, 9, 9));
        }

        private void BeginButton_Click(object sender, RoutedEventArgs e)
        {
            player.SeekAbsolute(0);
        }

        private void StepBackButton_Click(object sender, RoutedEventArgs e)
        {
            DoPause();
            player.StepBack();
        }

        private void StepForwardButton_Click(object sender, RoutedEventArgs e)
        {
            DoPause();
            player.Step();
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            player.SeekAbsolute(-1);
        }

        private void MarkInButton_Click(object sender, RoutedEventArgs e)
        {
            MarkIn = player.GetCurrentPos();
            SeekBar.IsSelectionRangeEnabled = true;
            SeekBar.SelectionStart = (double)MarkIn / (double)player.GetEndPosition();
        }

        private void MarkOutButton_Click(object sender, RoutedEventArgs e)
        {
            MarkOut = player.GetCurrentPos();
            SeekBar.IsSelectionRangeEnabled = true;
            SeekBar.SelectionEnd = (double)MarkOut / (double)player.GetEndPosition();
        }

        private void SeekBarTimer_Tick(object sender, EventArgs e)
        {
            Int64 currPos = player.GetCurrentPos();
            Int64 endPos = player.GetEndPosition();

            double scalarDone = (double)currPos / (double)endPos;

            SeekBar.Value = scalarDone;
        }

        private void SeekBar_DragStarted(object sender, DragStartedEventArgs e)
        {
            DoPause();
            SeekBarTimer.Stop();
        }

        private void SeekBar_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            long newPos = (long)(SeekBar.Value * player.GetEndPosition());
            player.SeekAbsolute(newPos);
            DoPlay();
            SeekBarTimer.Start();
        }

        private void SeekBar_DragDelta(object sender, DragDeltaEventArgs e)
        {
            long newPos = (long)(SeekBar.Value * player.GetEndPosition());
            player.SeekAbsolute(newPos);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (player == null) return;
            player.SetVolume((int)e.NewValue);
        }
    }
}
