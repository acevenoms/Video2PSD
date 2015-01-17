using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using DirectShowLib;

namespace Video2PSD
{
    /// <summary>
    /// A high quality, DirectShow based video player that uses the LibAV/DirectVobSub/madVR graph used by MPC-HC in KCP
    /// </summary>
    class HQDShowPlayer : IDisposable
    {
        private Control RenderWindow;
        private string FileName;

        private IMediaEvent Events;
        private ManualResetEvent MRE;

        private IFilterGraph2 MyFilterGraph = null;
        private DsROTEntry ROTEntry;
        private IMediaControl MyGraphController = null;

        private bool Closing = false;

        //Filters of the graph
        private IBaseFilter File = null;
        private IBaseFilter LAVSplitter = null;
        private IBaseFilter LAVVideoDecoder = null;
        private IBaseFilter LAVAudioDecoder = null;
        private IBaseFilter DirectVobSub = null;
        private IBaseFilter madVR = null;
        private IBaseFilter DefaultDirectSound = null;

        public HQDShowPlayer(Control renderWnd)
        {
            RenderWindow = renderWnd;
            RenderWindow.SizeChanged += VideoWindowSizeChanged;
        }

        public void Dispose()
        {
            Stop();

            lock (this)
            {
                Closing = true;
            }
            if (MRE != null) MRE.Set();
            
            TeardownGraph();
        }

        public bool OpenFile(string pathToMedia)
        {
            if (GetState() != FilterState.Stopped)
                Stop();

            TeardownGraph();

            FileName = pathToMedia;

            BuildGraph();

            IntPtr hEvent;
            int hr;

            hr = Events.GetEventHandle(out hEvent);
            DsError.ThrowExceptionForHR(hr);

            MRE = new ManualResetEvent(false);
            MRE.SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(hEvent, true);

            if (Closing) Closing = false;

            Thread t = new Thread(this.EventWait);
            t.Name = "Media Event Thread";
            t.Start();

            return true;
        }

        public void Play() 
        {
            if (GetState() != FilterState.Running && MyGraphController != null)
            {
                int hr = MyGraphController.Run();
                DsError.ThrowExceptionForHR(hr);
            }
        }
        public void Pause() 
        {
            if (GetState() != FilterState.Paused && MyGraphController != null)
            {
                int hr = MyGraphController.Pause();
                DsError.ThrowExceptionForHR(hr);
            }
        }
        public void Stop() 
        {
            if (GetState() != FilterState.Stopped)
            {
                int hr = MyGraphController.Stop();
                DsError.ThrowExceptionForHR(hr);
            }
        }
        public FilterState GetState() 
        {
            if (MyGraphController == null) return FilterState.Stopped;

            FilterState state;
            int hr;

            hr = MyGraphController.GetState(0, out state);
            DsError.ThrowExceptionForHR(hr);

            return state;
        } 

        public void Step() 
        {
            if (MyFilterGraph == null) return;
            if (GetState() != FilterState.Paused) Pause();

            //Int64 prevTime, currTime;

            //prevTime = GetCurrentPos();
            IVideoFrameStep vfs = MyFilterGraph as IVideoFrameStep;
            DsError.ThrowExceptionForHR(vfs.Step(1, null));
            //currTime = GetCurrentPos();
            //Debug.WriteLine("Stepped once: {0} MT", currTime - prevTime);
        }
        public void StepBack() 
        {
            if (MyFilterGraph == null) return;
            if (GetState() != FilterState.Paused) Pause();

            int hr;
            IMediaSeeking graphSeek = MyFilterGraph as IMediaSeeking;
            IBasicVideo madVRVideo = madVR as IBasicVideo;
            double timePerFrame;
            hr = madVRVideo.get_AvgTimePerFrame(out timePerFrame);
            DsError.ThrowExceptionForHR(hr);

            //Seconds to TimeSteps
            long frameTime = (long)Math.Round(timePerFrame * 1000.0);
            //TimeSteps to MediaTime
            frameTime *= 10000;

            long currPos, stopPos;
            hr = graphSeek.GetPositions(out currPos, out stopPos);
            DsError.ThrowExceptionForHR(hr);
            hr = graphSeek.SetPositions(currPos - frameTime, AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);
            DsError.ThrowExceptionForHR(hr);
        }

        public Int64 GetEndPosition()
        {
            int hr;
            long pos;
            IMediaSeeking graphSeek = MyFilterGraph as IMediaSeeking;
            hr = graphSeek.GetStopPosition(out pos);
            DsError.ThrowExceptionForHR(hr);
            return pos; 
        }

        public Int64 GetCurrentPos()
        {
            int hr;
            long pos;
            IMediaSeeking graphSeek = MyFilterGraph as IMediaSeeking;
            hr = graphSeek.GetCurrentPosition(out pos);
            DsError.ThrowExceptionForHR(hr);
            //hr = graphSeek.ConvertTimeFormat(out frame, TimeFormat.Frame, pos, TimeFormat.MediaTime);
            //DsError.ThrowExceptionForHR(hr);
            //Debug.WriteLine("Position from Graph: Media Time: {0}", pos);
            return pos;
        }

        public void SeekAbsolute(Int64 pos) 
        {
            int hr;
            IMediaSeeking graphSeek = MyFilterGraph as IMediaSeeking;
            hr = graphSeek.SetPositions(pos, AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);
            DsError.ThrowExceptionForHR(hr);
        }

        public void SetVolume(int Volume)
        {
            int hr;
            IBasicAudio DSoundAudio = DefaultDirectSound as IBasicAudio;
            int cdB; //centidecibels
            if (Volume == 0) //The function is asymptotic at x=0
                cdB = -10000;
            else
                cdB = (int)((20 * Math.Log10(Volume / 100.0)) * 100);
            hr = DSoundAudio.put_Volume(cdB);
            DsError.ThrowExceptionForHR(hr);
        }

        public List<string> GetSubtitleTracks()
        {
            int hr;
            IAMStreamSelect demuxStreamSelect = LAVSplitter as IAMStreamSelect;
            List<string> streams = new List<string>();

            int nStreams;
            hr = demuxStreamSelect.Count(out nStreams);
            DsError.ThrowExceptionForHR(hr);

            for (int i = 0; i < nStreams; ++i)
            {
                AMMediaType type = new AMMediaType();
                AMStreamSelectInfoFlags enabled;
                string name;
                int localeId, groupId;
                object obj = null, unk = null;
                hr = demuxStreamSelect.Info(i, out type, out enabled, out localeId, out groupId, out name, out obj, out unk);
                DsError.ThrowExceptionForHR(hr);

                streams.Add(name);
            }

            return streams;
        }

        public bool ToggleSubtitle(Nullable<bool> enable = null) { return true; }

        public Image GetCapture() { return new Bitmap(0, 0); }

        private void VideoWindowSizeChanged(object sender, EventArgs e) 
        {
            if (madVR == null) return;
            int hr;
            IBasicVideo madVRVideo = madVR as IBasicVideo;
            IVideoWindow madVRWindow = madVR as IVideoWindow;

            int sourceW, sourceH;
            hr = madVRVideo.get_VideoWidth(out sourceW);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRVideo.get_VideoHeight(out sourceH);
            DsError.ThrowExceptionForHR(hr);

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)RenderWindow.ClientSize.Width / (float)sourceW);
            nPercentH = ((float)RenderWindow.ClientSize.Height / (float)sourceH);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceW * nPercent);
            int destHeight = (int)(sourceH * nPercent);

            hr = madVRVideo.put_DestinationWidth(destWidth);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRVideo.put_DestinationHeight(destHeight);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.SetWindowPosition((RenderWindow.ClientSize.Width - destWidth) / 2, (RenderWindow.ClientSize.Height - destHeight) / 2, destWidth, destHeight);
            DsError.ThrowExceptionForHR(hr);
        }

        private void BuildGraph() 
        {
            int hr;

            MyFilterGraph = new FilterGraph() as IFilterGraph2;
            ROTEntry = new DsROTEntry(MyFilterGraph);

            //File source
            File = new AsyncReader() as IBaseFilter;
            hr = MyFilterGraph.AddFilter(File, "File Source (Async.)");
            DsError.ThrowExceptionForHR(hr);
            (File as IFileSourceFilter).Load(FileName, new AMMediaType());

            //LAV Splitter
            LAVSplitter = new LAVSplitter() as IBaseFilter; //I need to actually create this filter somehow... it doesn't look promising
            hr = MyFilterGraph.AddFilter(LAVSplitter, "LAV Splitter");
            DsError.ThrowExceptionForHR(hr);

            IPin fileOut;
            hr = File.FindPin("Output", out fileOut);
            DsError.ThrowExceptionForHR(hr);
            IPin LAVSplitIn;
            hr = LAVSplitter.FindPin("Input", out LAVSplitIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(fileOut, LAVSplitIn);
            DsError.ThrowExceptionForHR(hr);
            Marshal.ReleaseComObject(fileOut);
            Marshal.ReleaseComObject(LAVSplitIn);

            //LAV Video Decoder
            LAVVideoDecoder = new LAVVideoDecoder() as IBaseFilter;
            hr = MyFilterGraph.AddFilter(LAVVideoDecoder, "LAV Video Decoder");
            DsError.ThrowExceptionForHR(hr);

            //LAV Audio Decoder
            LAVAudioDecoder = new LAVAudioDecoder() as IBaseFilter;
            hr = MyFilterGraph.AddFilter(LAVAudioDecoder, "LAV Audio Decoder");
            DsError.ThrowExceptionForHR(hr);

            //DirectVobSub
            DirectVobSub = new DirectVobSub() as IBaseFilter;
            hr = MyFilterGraph.AddFilter(DirectVobSub, "DirectVobSub (auto-loading version)");
            DsError.ThrowExceptionForHR(hr);

            IPin compressedVideoOut;
            hr = LAVSplitter.FindPin("Video", out compressedVideoOut);
            DsError.ThrowExceptionForHR(hr);
            IPin compressedAudioOut;
            hr = LAVSplitter.FindPin("Audio", out compressedAudioOut);
            DsError.ThrowExceptionForHR(hr);
            IPin subsOut;
            hr = LAVSplitter.FindPin("Subtitle", out subsOut);
            DsError.ThrowExceptionForHR(hr);
            IPin decompressedVideoOut;
            hr = LAVVideoDecoder.FindPin("Out", out decompressedVideoOut);
            DsError.ThrowExceptionForHR(hr);
            IPin compressedVideoIn;
            hr = LAVVideoDecoder.FindPin("In", out compressedVideoIn);
            DsError.ThrowExceptionForHR(hr);
            IPin compressedAudioIn;
            hr = LAVAudioDecoder.FindPin("In", out compressedAudioIn);
            DsError.ThrowExceptionForHR(hr);

            IPin subsIn = null;
            // For some reason, normal method of finding pins doesn't work on DirectVobSub, so we manually loop and query for it.
            //hr = DirectVobSub.FindPin("Input", out subsIn);
            //DsError.ThrowExceptionForHR(hr);

            IEnumPins pins;
            IPin [] tempPins = new IPin[1];
            DirectVobSub.EnumPins(out pins);
            while (pins.Next(1, tempPins, IntPtr.Zero) == 0)
            {
                string pinName;
                PinDirection pinDir;
                tempPins[0].QueryId(out pinName);
                tempPins[0].QueryDirection(out pinDir);

                if (pinName == "Input") subsIn = tempPins[0];
                //Debug.Print("Pin Found: {0} {1}", pinDir == PinDirection.Input ? ">" : "<", pinName);
            }

            IPin decompressedVideoIn;
            hr = DirectVobSub.FindPin("In", out decompressedVideoIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(compressedVideoOut, compressedVideoIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(decompressedVideoOut, decompressedVideoIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(compressedAudioOut, compressedAudioIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(subsOut, subsIn);
            DsError.ThrowExceptionForHR(hr);
            Marshal.ReleaseComObject(compressedVideoOut);
            Marshal.ReleaseComObject(compressedVideoIn);
            Marshal.ReleaseComObject(decompressedVideoOut);
            Marshal.ReleaseComObject(decompressedVideoIn);
            Marshal.ReleaseComObject(compressedAudioOut);
            Marshal.ReleaseComObject(compressedAudioIn);
            Marshal.ReleaseComObject(subsOut);
            Marshal.ReleaseComObject(subsIn);

            //madVR
            madVR = new madVR() as IBaseFilter;
            hr = MyFilterGraph.AddFilter(madVR, "madVR");
            DsError.ThrowExceptionForHR(hr);
            IVideoWindow madVRWindow = madVR as IVideoWindow;

            IPin subbedVideoOut;
            hr = DirectVobSub.FindPin("Out", out subbedVideoOut);
            DsError.ThrowExceptionForHR(hr);
            IPin subbedVideoIn;
            hr = madVR.FindPin("In", out subbedVideoIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(subbedVideoOut, subbedVideoIn);
            DsError.ThrowExceptionForHR(hr);
            Marshal.ReleaseComObject(subbedVideoOut);
            Marshal.ReleaseComObject(subbedVideoIn);

            //madVR must be configured after connecting the pins
            hr = madVRWindow.put_Owner(RenderWindow.Handle);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipChildren | WindowStyle.ClipSiblings);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.put_Visible(OABool.True);
            DsError.ThrowExceptionForHR(hr);
            VideoWindowSizeChanged(this, new EventArgs());

            //Sound render
            DefaultDirectSound = new DSoundRender() as IBaseFilter;
            MyFilterGraph.AddFilter(DefaultDirectSound, "Default Direct Sound Device");

            IPin decompressedAudioOut;
            hr = LAVAudioDecoder.FindPin("Out", out decompressedAudioOut);
            DsError.ThrowExceptionForHR(hr);
            IPin decompressedAudioIn;
            hr = DefaultDirectSound.FindPin("Audio Input pin (rendered)", out decompressedAudioIn);
            DsError.ThrowExceptionForHR(hr);
            hr = MyFilterGraph.Connect(decompressedAudioOut, decompressedAudioIn);
            DsError.ThrowExceptionForHR(hr);
            Marshal.ReleaseComObject(decompressedAudioOut);
            Marshal.ReleaseComObject(decompressedAudioIn);

            IBasicAudio DSoundAudio = DefaultDirectSound as IBasicAudio;
            SetVolume(50);

            Events = MyFilterGraph as IMediaEvent;
            MyGraphController = MyFilterGraph as IMediaControl;
        }
        private void TeardownGraph() 
        {
            if (MyFilterGraph == null || MyGraphController == null) return;
            if (GetState() != FilterState.Stopped) Stop();

            if (Events != null) Marshal.ReleaseComObject(Events);
            if (madVR != null) Marshal.ReleaseComObject(madVR);
            if (DirectVobSub != null) Marshal.ReleaseComObject(DirectVobSub);
            if (LAVAudioDecoder != null) Marshal.ReleaseComObject(LAVAudioDecoder);
            if (LAVVideoDecoder != null) Marshal.ReleaseComObject(LAVVideoDecoder);
            if (LAVSplitter != null) Marshal.ReleaseComObject(LAVSplitter);
            if (File != null) Marshal.ReleaseComObject(File);

            if (ROTEntry != null) ROTEntry.Dispose();

            if (MyFilterGraph != null) Marshal.ReleaseComObject(MyFilterGraph);
            MyGraphController = null;
        }

        private void EventWait() 
        {
            // Returned when GetEvent is called but there are no events
            const int E_ABORT = unchecked((int)0x80004004);

            int hr;
            IntPtr p1, p2;
            EventCode ec;

            do
            {
                MRE.WaitOne(-1, true);

                lock (this)
                {
                    if (!Closing)
                    {
                        for (hr = Events.GetEvent(out ec, out p1, out p2, 0);
                            hr >= 0;
                            hr = Events.GetEvent(out ec, out p1, out p2, 0))
                        {
                            Debug.WriteLine(ec.ToString());

                            if (ec == EventCode.Complete)
                            {
                                Stop();
                            }

                            // Release any resources the message allocated
                            hr = Events.FreeEventParams(ec, p1, p2);
                            DsError.ThrowExceptionForHR(hr);
                        }

                        if (hr != E_ABORT)
                        {
                            DsError.ThrowExceptionForHR(hr);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            } while (true);
        }
    }
}
