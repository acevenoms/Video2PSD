using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using DirectShowLib;

namespace Video2PSD
{
    /// <summary>
    /// A high quality, DirectShow based video player that uses the LibAV/DirectVobSub/madVR graph used by MPC-HC in KCP
    /// </summary>
    class HQDShowPlayer
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

        public HQDShowPlayer(Control renderWnd)
        {
            RenderWindow = renderWnd;
            RenderWindow.SizeChanged += VideoWindowSizeChanged;
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

            Thread t = new Thread(this.EventWait);
            t.Name = "Media Event Thread";
            t.Start();

            return true;
        }

        public void Play() 
        {
            if (GetState() != FilterState.Running)
            {
                int hr = MyGraphController.Run();
                DsError.ThrowExceptionForHR(hr);
            }
        }
        public void Pause() 
        {
            if (GetState() != FilterState.Paused)
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

        public void Step() { }
        public void StepBack() { }

        public void SeekAbsolute() { }
        public bool ToggleSubtitle(Nullable<bool> enable = null) { return true; }

        public Image GetCapture() { return new Bitmap(0, 0); }

        private void VideoWindowSizeChanged(object sender, EventArgs e) 
        {
            int hr;
            IBasicVideo madVRVideo = madVR as IBasicVideo;
            IVideoWindow madVRWindow = madVR as IVideoWindow;
            hr = madVRVideo.put_DestinationWidth(RenderWindow.Width);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRVideo.put_DestinationHeight(RenderWindow.Height);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.SetWindowPosition(0, 0, RenderWindow.ClientSize.Width, RenderWindow.ClientSize.Height);
            DsError.ThrowExceptionForHR(hr);
        }

        private void BuildGraph() 
        {
            int hr;

            MyFilterGraph = new FilterGraph() as IFilterGraph2;

            ICaptureGraphBuilder2 icgb2 = new CaptureGraphBuilder2() as ICaptureGraphBuilder2;

            hr = icgb2.SetFiltergraph(MyFilterGraph);
            DsError.ThrowExceptionForHR(hr);

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

            //madVR must be configured after connecting the pins
            hr = madVRWindow.put_Owner(RenderWindow.Handle);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipChildren | WindowStyle.ClipSiblings);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.put_Visible(OABool.True);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRWindow.SetWindowPosition(0, 0, RenderWindow.ClientSize.Width, RenderWindow.ClientSize.Height);
            DsError.ThrowExceptionForHR(hr);

            IBasicVideo madVRVideo = madVR as IBasicVideo;
            hr = madVRVideo.put_DestinationWidth(RenderWindow.Width);
            DsError.ThrowExceptionForHR(hr);
            hr = madVRVideo.put_DestinationHeight(RenderWindow.Height);
            DsError.ThrowExceptionForHR(hr);

            Events = MyFilterGraph as IMediaEvent;
            MyGraphController = MyFilterGraph as IMediaControl;
        }
        private void TeardownGraph() { }

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
                            Debug.Write(ec.ToString());

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
