﻿using System;
using System.Runtime.InteropServices;
using Pvp.Core.DirectShow;
using Pvp.Core.MediaEngine.Description;
using Pvp.Core.MediaEngine.Renderers;
using Pvp.Core.Native;

namespace Pvp.Core.MediaEngine.FilterGraphs
{
    internal class RegularFilterGraph : FilterGraphBase
    {
        private readonly SourceType _sourceType;
        private readonly Guid _recommendedSourceFilterId;

        private ISourceFilterHandler _sourceFilterHandler;

        public RegularFilterGraph(SourceType sourceType, Guid recommendedSourceFilterId)
        {
            _recommendedSourceFilterId = recommendedSourceFilterId;
            _sourceType = sourceType;

            _aspectRatio = 1.0;
            _rate = 1.00;
        }

        public override void BuildUp(FilterGraphBuilderParameters parameters)
        {
            // Create filter graph manager
            InitializeGraphBuilder(() =>
                                       {
                                           object comobj = null;
                                           try
                                           {
                                               var type = Type.GetTypeFromCLSID(Clsid.FilterGraph, true);
                                               comobj = Activator.CreateInstance(type);
                                               var graphBuilder = (IGraphBuilder)comobj;
                                               comobj = null; // important! (see the finally block)

                                               return graphBuilder;
                                           }
                                           catch (Exception e)
                                           {
                                               throw new FilterGraphBuilderException(GraphBuilderError.FilterGraphManager, e);
                                           }
                                           finally
                                           {
                                               if (comobj != null)
                                               {
                                                   Marshal.FinalReleaseComObject(comobj);
                                               }
                                           }
                                       });
            

            // Adding a source filter for a specific video file
            _sourceFilterHandler = SourceFilterHandlerFactory.AddSourceFilter(GraphBuilder, parameters.Source);

            // QUERY the filter graph interfaces
            InitializeMediaEventEx(parameters.MediaWindowHandle);
            InitializeFilterGraph2();
            InitializeMediaControl();
            InitializeMediaSeeking();
            InitializeBasicAudio();

            // create a renderer
            ThrowExceptionForHRPointer errorFunc = delegate(int hrCode, GraphBuilderError error)
                                                       {
                                                           hrCode.ThrowExceptionForHR(error);
                                                       };

            Renderer = RendererBase.AddRenderer(GraphBuilder, parameters.PreferredVideoRenderer, errorFunc, parameters.MediaWindowHandle);
            
            DoBuildGraph();

            SeekingCapabilities caps = SeekingCapabilities.CanGetDuration;
            int hr = MediaSeeking.CheckCapabilities(ref caps);
            if (hr == DsHlp.S_OK)
            {
                _isGraphSeekable = true;

                long rtDuration;
                MediaSeeking.GetDuration(out rtDuration);

                _duration = rtDuration;
            }

            // MEDIA SIZE
            int height, arWidth;
            int width, arHeight;

            Renderer.GetNativeVideoSize(out width, out height, out arWidth, out arHeight);

            double w = arWidth;
            double h = arHeight;
            _aspectRatio = w / h;

            _sourceRect = new GDI.RECT { left = 0, top = 0, right = width, bottom = height };
            
//            if (FindSplitter(pFilterGraph))
//                ReportUnrenderedPins(pFilterGraph); // then we can raise OnFailedStreamsAvailable
            GatherMediaInfo(parameters.Source);
        }

        public override SourceType SourceType
        {
            get { return _sourceType; }
        }

        private bool _isGraphSeekable;
        public override bool IsGraphSeekable
        {
            get { return _isGraphSeekable; }
        }

        private long _duration;
        public override long Duration
        {
            get { return _duration; }
        }

        private double _rate;
        public override double Rate
        {
            get { return _rate; }
        }

        public override int AudioStreamsCount
        {
            get { return _sourceFilterHandler.AudioStreamsCount; }
        }

        public override int CurrentAudioStream
        {
            get { return _sourceFilterHandler.CurrentAudioStream; }
            set { _sourceFilterHandler.CurrentAudioStream = value; }
        }

        private GDI.RECT _sourceRect;
        public override GDI.RECT SourceRect
        {
            get { return _sourceRect; }
        }

        private double _aspectRatio;
        public override double AspectRatio
        {
            get { return _aspectRatio; }
        }

        public override bool SetVolume(int volume)
        {
            return _sourceFilterHandler.SetVolume(volume);
        }

        public override bool GetVolume(out int volume)
        {
            return _sourceFilterHandler.GetVolume(out volume);
        }

        protected override void OnExternalStreamSelection()
        {
            _sourceFilterHandler.OnExternalStreamSelection();
        }

        public override int NumberOfSubpictureStreams
        {
            get { return _sourceFilterHandler.NumberOfSubpictureStreams; }
        }

        public override int CurrentSubpictureStream
        {
            get { return _sourceFilterHandler.CurrentSubpictureStream; }
            set { _sourceFilterHandler.CurrentSubpictureStream = value; }
        }

        public override bool EnableSubpicture(bool bEnable)
        {
            return _sourceFilterHandler.EnableSubpicture(bEnable);
        }

        public override string GetSubpictureStreamName(int nStream)
        {
            return _sourceFilterHandler.GetSubpictureStreamName(nStream);
        }

        public override bool IsSubpictureEnabled()
        {
            return _sourceFilterHandler.IsSubpictureEnabled();
        }

        public override bool IsSubpictureStreamEnabled(int ulStreamNum)
        {
            return _sourceFilterHandler.IsSubpictureStreamEnabled(ulStreamNum);
        }

        public override string GetAudioStreamName(int nStream)
        {
            return _sourceFilterHandler.GetAudioStreamName(nStream);
        }

        public override long GetCurrentPosition()
        {
            var result = 0L;

            if (IsGraphSeekable)
            {
                MediaSeeking.GetCurrentPosition(out result);
            }

            return result;
        }

        public override void SetCurrentPosition(long time)
        {
            if (IsGraphSeekable)
            {
                var state = GraphState;
                PauseGraph();
                long pStop = 0;
                MediaSeeking.SetPositions(ref time, SeekingFlags.AbsolutePositioning, ref pStop, SeekingFlags.NoPositioning);

                switch (state)
                {
                    case GraphState.Running:
                        ResumeGraph();
                        break;
                    case GraphState.Stopped:
                        MediaControl.Stop();
                        GraphState = GraphState.Stopped;
                        break;
                }
            }
        }

        public override void SetRate(double rate)
        {
            if (IsGraphSeekable)
            {
                var hr = MediaSeeking.SetRate(rate);
                if (hr == DsHlp.S_OK)
                {
                    _rate = rate;
                }
            }
        }

        protected override void CloseInterfaces()
        {
            base.CloseInterfaces();

            if (_sourceFilterHandler != null)
            {
                _sourceFilterHandler.Dispose();
            }
        }

        private void DoBuildGraph()
        {
            _sourceFilterHandler.RenderVideo(GraphBuilder, Renderer);
            _sourceFilterHandler.RenderAudio(GraphBuilder);
            _sourceFilterHandler.RenderSubpicture(GraphBuilder, Renderer);
        }

//        public IEnumerable<StreamInfo> ReportUnrenderedPins()
//        {
//            int hr;
//            IntPtr ptr;
//            IEnumMediaTypes pEnumTypes;
//            int cFetched;
//            IPin pPin;
//
//            StreamInfo pStreamInfo;
//            int nPinsToSkip = 0;
//
//            IList<StreamInfo> streams = new List<StreamInfo>();
//
//            while ((pPin = DsUtils.GetPin(_splitterFilter, PinDirection.Output, false, nPinsToSkip)) != null)
//            {
//                nPinsToSkip++;
//                pStreamInfo = new StreamInfo();
//                hr = pPin.EnumMediaTypes(out pEnumTypes);
//                if (hr == DsHlp.S_OK)
//                {
//                    if (pEnumTypes.Next(1, out ptr, out cFetched) == DsHlp.S_OK)
//                    {
//                        AMMediaType mt = (AMMediaType)Marshal.PtrToStructure(ptr, typeof(AMMediaType));
//                        GatherStreamInfo(pGraph, pStreamInfo, ref mt);
//
//                        DsUtils.FreeFormatBlock(ptr);
//                        Marshal.FreeCoTaskMem(ptr);
//                    }
//                    Marshal.ReleaseComObject(pEnumTypes);
//                }
//                Marshal.ReleaseComObject(pPin);
//                streams.Add(pStreamInfo);
//            }
//
//            return streams;
//        }

        private void GatherMediaInfo(string source)
        {
            AddToMediaInfo(source);

            if (SourceType == SourceType.Asf)
            {
                AddToMediaInfo(MediaSubType.Asf);
            }
            else
            {
                _sourceFilterHandler.GetMainStreamSubtype(mt => AddToMediaInfo(mt.subType));
            }

            _sourceFilterHandler.GetStreamsMediaTypes(mt =>
                                                          {
                                                              var streamInfo = GatherStreamInfo(mt);
                                                              AddToMediaInfo(streamInfo);
                                                          });
        }

        private int GetVideoDimension(int value1, int value2)
        {
            int value = value1 != 0 ? value1 : value2;
            if (value < 0)
                value *= -1;
            return value;
        }

        private StreamInfo GatherStreamInfo(AMMediaType pmt)
        {
            var streamInfo = new StreamInfo();

            streamInfo.MajorType = pmt.majorType;
            streamInfo.SubType = pmt.subType;
            streamInfo.FormatType = pmt.formatType;

            if (pmt.formatType == FormatType.VideoInfo)
            {
                // Check the buffer size.
                if (pmt.formatSize >= Marshal.SizeOf(typeof(VIDEOINFOHEADER)))
                {
                    VIDEOINFOHEADER pVih = (VIDEOINFOHEADER)Marshal.PtrToStructure(pmt.formatPtr, typeof(VIDEOINFOHEADER));
                    streamInfo.dwBitRate = pVih.dwBitRate;
                    streamInfo.AvgTimePerFrame = pVih.AvgTimePerFrame;
                    streamInfo.Flags = StreamInfoFlags.SI_VIDEOBITRATE | StreamInfoFlags.SI_FRAMERATE;

                    streamInfo.rcSrc.right = GetVideoDimension(SourceRect.right, pVih.bmiHeader.biWidth);
                    streamInfo.rcSrc.bottom = GetVideoDimension(SourceRect.bottom, pVih.bmiHeader.biHeight);
                }
                else
                {
                    streamInfo.rcSrc.right = SourceRect.right;
                    streamInfo.rcSrc.bottom = SourceRect.bottom;
                }

                streamInfo.Flags |= (StreamInfoFlags.SI_RECT | StreamInfoFlags.SI_FOURCC);
            }
            else if (pmt.formatType == FormatType.VideoInfo2)
            {
                // Check the buffer size.
                if (pmt.formatSize >= Marshal.SizeOf(typeof(VIDEOINFOHEADER2)))
                {
                    VIDEOINFOHEADER2 pVih2 = (VIDEOINFOHEADER2)Marshal.PtrToStructure(pmt.formatPtr, typeof(VIDEOINFOHEADER2));
                    streamInfo.dwBitRate = pVih2.dwBitRate;
                    streamInfo.AvgTimePerFrame = pVih2.AvgTimePerFrame;
                    streamInfo.dwPictAspectRatioX = pVih2.dwPictAspectRatioX;
                    streamInfo.dwPictAspectRatioY = pVih2.dwPictAspectRatioY;
                    streamInfo.dwInterlaceFlags = pVih2.dwInterlaceFlags;
                    streamInfo.Flags = StreamInfoFlags.SI_VIDEOBITRATE |
                        StreamInfoFlags.SI_FRAMERATE | StreamInfoFlags.SI_ASPECTRATIO |
                        StreamInfoFlags.SI_INTERLACEMODE;

                    streamInfo.rcSrc.right = GetVideoDimension(SourceRect.right, pVih2.bmiHeader.biWidth);
                    streamInfo.rcSrc.bottom = GetVideoDimension(SourceRect.bottom, pVih2.bmiHeader.biHeight);
                }
                else
                {
                    streamInfo.rcSrc.right = SourceRect.right;
                    streamInfo.rcSrc.bottom = SourceRect.bottom;
                }

                streamInfo.Flags |= (StreamInfoFlags.SI_RECT | StreamInfoFlags.SI_FOURCC);
            }
            else if (pmt.formatType == FormatType.WaveEx)
            {
                // Check the buffer size.
                if (pmt.formatSize >= /*Marshal.SizeOf(typeof(WAVEFORMATEX))*/ 18)
                {
                    WAVEFORMATEX pWfx = (WAVEFORMATEX)Marshal.PtrToStructure(pmt.formatPtr, typeof(WAVEFORMATEX));
                    streamInfo.wFormatTag = pWfx.wFormatTag;
                    streamInfo.nSamplesPerSec = pWfx.nSamplesPerSec;
                    streamInfo.nChannels = pWfx.nChannels;
                    streamInfo.wBitsPerSample = pWfx.wBitsPerSample;
                    streamInfo.nAvgBytesPerSec = pWfx.nAvgBytesPerSec;
                    streamInfo.Flags = StreamInfoFlags.SI_WAVEFORMAT |
                        StreamInfoFlags.SI_SAMPLERATE | StreamInfoFlags.SI_WAVECHANNELS |
                        StreamInfoFlags.SI_BITSPERSAMPLE | StreamInfoFlags.SI_AUDIOBITRATE;
                }
            }
            else if (pmt.formatType == FormatType.MpegVideo)
            {
                // Check the buffer size.
                if (pmt.formatSize >= Marshal.SizeOf(typeof(MPEG1VIDEOINFO)))
                {
                    MPEG1VIDEOINFO pM1vi = (MPEG1VIDEOINFO)Marshal.PtrToStructure(pmt.formatPtr, typeof(MPEG1VIDEOINFO));
                    streamInfo.dwBitRate = pM1vi.hdr.dwBitRate;
                    streamInfo.AvgTimePerFrame = pM1vi.hdr.AvgTimePerFrame;
                    streamInfo.Flags = StreamInfoFlags.SI_VIDEOBITRATE | StreamInfoFlags.SI_FRAMERATE;

                    streamInfo.rcSrc.right = GetVideoDimension(SourceRect.right, pM1vi.hdr.bmiHeader.biWidth);
                    streamInfo.rcSrc.bottom = GetVideoDimension(SourceRect.bottom, pM1vi.hdr.bmiHeader.biHeight);
                }
                else
                {
                    streamInfo.rcSrc.right = SourceRect.right;
                    streamInfo.rcSrc.bottom = SourceRect.bottom;
                }

                streamInfo.Flags |= (StreamInfoFlags.SI_RECT | StreamInfoFlags.SI_FOURCC);
            }
            else if (pmt.formatType == FormatType.Mpeg2Video)
            {
                // Check the buffer size.
                if (pmt.formatSize >= Marshal.SizeOf(typeof(MPEG2VIDEOINFO)))
                {
                    MPEG2VIDEOINFO pM2vi = (MPEG2VIDEOINFO)Marshal.PtrToStructure(pmt.formatPtr, typeof(MPEG2VIDEOINFO));
                    streamInfo.dwBitRate = pM2vi.hdr.dwBitRate;
                    streamInfo.AvgTimePerFrame = pM2vi.hdr.AvgTimePerFrame;
                    streamInfo.dwPictAspectRatioX = pM2vi.hdr.dwPictAspectRatioX;
                    streamInfo.dwPictAspectRatioY = pM2vi.hdr.dwPictAspectRatioY;
                    streamInfo.dwInterlaceFlags = pM2vi.hdr.dwInterlaceFlags;
                    streamInfo.Flags = StreamInfoFlags.SI_VIDEOBITRATE | StreamInfoFlags.SI_FRAMERATE |
                        StreamInfoFlags.SI_ASPECTRATIO | StreamInfoFlags.SI_INTERLACEMODE;

                    streamInfo.rcSrc.right = GetVideoDimension(SourceRect.right, pM2vi.hdr.bmiHeader.biWidth);
                    streamInfo.rcSrc.bottom = GetVideoDimension(SourceRect.bottom, pM2vi.hdr.bmiHeader.biHeight);
                }
                else
                {
                    streamInfo.rcSrc.right = SourceRect.right;
                    streamInfo.rcSrc.bottom = SourceRect.bottom;
                }

                streamInfo.Flags |= (StreamInfoFlags.SI_RECT | StreamInfoFlags.SI_FOURCC);
            }

            return streamInfo;
        }
    }
}