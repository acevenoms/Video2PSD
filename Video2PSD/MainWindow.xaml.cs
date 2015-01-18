﻿using System;
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

        private bool Muted = false;

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

                RepopulateStreamMenu();
            }
        }

        private void RepopulateStreamMenu()
        {
            SubTracksMenu.Items.Clear();

            HQDShowPlayer.StreamCollection streams = player.GetSubtitleTracks();
            foreach (HQDShowPlayer.StreamGroup sg in streams.Groups)
            {
                foreach (HQDShowPlayer.Stream s in sg.Streams)
                {
                    MenuItem item = new MenuItem();
                    item.Header = s.Name;
                    item.IsCheckable = true;
                    item.IsChecked = (s.SelectFlags != DirectShowLib.AMStreamSelectInfoFlags.Disabled);
                    item.Checked += (object sender2, RoutedEventArgs e2) =>
                    {
                        player.EnableStream(s);

                        RepopulateStreamMenu();
                    };
                    SubTracksMenu.Items.Add(item);
                }
                SubTracksMenu.Items.Add(new Separator());
            }
            SubTracksMenu.Items.RemoveAt(SubTracksMenu.Items.Count - 1);
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
            TimeSpan current, end;
            current = new TimeSpan(currPos);
            end = new TimeSpan(endPos);
            TimeCodeDisplay.Content = string.Format("{0:00}:{1:00}.{2:000}/{3:00}:{4:00}.{5:000}",
                (int)current.TotalMinutes, current.Seconds, current.Milliseconds,
                (int)end.TotalMinutes, end.Seconds, end.Milliseconds);
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

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Muted)
            {
                player.SetVolume((int)VolumeSlider.Value);

                Image buttonSprite = (MuteButton.Content as Image);

                buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(70, 0, 15, 15));

                Muted = false;
            }
            else
            {
                player.SetVolume(0);

                Image buttonSprite = (MuteButton.Content as Image);
                buttonSprite.Source = new CroppedBitmap((BitmapSource)this.Resources["SpriteSheet"], new Int32Rect(85, 0, 15, 15));

                Muted = true;
            }
        }

        private void SeekBar_Click(object sender, RoutedEventArgs e)
        {
            Point rawClickPoint = Mouse.GetPosition(SeekBar);
            double scalarDesiredLocation;
            if (rawClickPoint.X <= SeekBar.Margin.Left)
                scalarDesiredLocation = 0.0;
            else if (rawClickPoint.X >= (SeekBar.ActualWidth - SeekBar.Margin.Right))
                scalarDesiredLocation = 1.0;
            else
            {
                double adjustedClickX = rawClickPoint.X - SeekBar.Margin.Left;
                scalarDesiredLocation = adjustedClickX / (SeekBar.ActualWidth - (SeekBar.Margin.Left + SeekBar.Margin.Right));
            }

            long newPos = (long)(scalarDesiredLocation * player.GetEndPosition());
            player.SeekAbsolute(newPos);
        }
    }
}
