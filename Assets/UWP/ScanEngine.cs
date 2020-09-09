#if UNITY_WSA && !UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Perception.Spatial;
using Windows.UI.Input.Spatial;
using Windows.Foundation.Numerics;
using System.Numerics;
using Windows.Media.Devices.Core;

public class ScanEngine
{
    public TimeSpan PredictionFrequency = TimeSpan.FromMilliseconds(400);

    private MediaCapture CameraCapture;
    private MediaFrameReader CameraFrameReader;
    private SpatialLocator m_locator;
    private SpatialStationaryFrameOfReference m_referenceFrame;

    // private PhotoCapture photoCaptureObject = null;

    private Int64 FramesCaptured;

    IUnityScanScene UnityApp;


    public ScanEngine()
    { 
    }

    public async Task Inititalize(IUnityScanScene unityApp)
    {   
        System.Diagnostics.Debug.WriteLine("Inititalize");
        UnityApp = unityApp;

        SetHolographicSpace();
        await InitializeCameraCapture();
        await InitializeCameraFrameReader();
    }

    private void SetHolographicSpace()
    {   
        // from Holographic Face Tracking Example
        // Use the default SpatialLocator to track the motion of the device.
        m_locator = SpatialLocator.GetDefault();
        
        // The simplest way to render world-locked holograms is to create a stationary reference frame
        // when the app is launched. This is roughly analogous to creating a "world" coordinate system
        // with the origin placed at the device's position as the app is launched.
        m_referenceFrame = m_locator.CreateStationaryFrameOfReferenceAtCurrentLocation();

    }

    public void ResetReferenceFrame()
    {
        // Allow user to reset the referenceFrame's location
        m_referenceFrame = m_locator.CreateStationaryFrameOfReferenceAtCurrentLocation();
    }

    private async Task InitializeCameraCapture()
    {   
        System.Diagnostics.Debug.WriteLine("InitializeCameraCapture");
        CameraCapture = new MediaCapture();
        MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
        settings.StreamingCaptureMode = StreamingCaptureMode.Video;
        await CameraCapture.InitializeAsync(settings);
    }

    private async Task InitializeCameraFrameReader()
    {   
        System.Diagnostics.Debug.WriteLine("InitializeCameraFrameReader");
        var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync(); 
        MediaFrameSourceGroup selectedGroup = null;
        MediaFrameSourceInfo colorSourceInfo = null;

        foreach (var sourceGroup in frameSourceGroups)
        {
            foreach (var sourceInfo in sourceGroup.SourceInfos)
            {
                if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview
                    && sourceInfo.SourceKind == MediaFrameSourceKind.Color)
                {
                    colorSourceInfo = sourceInfo;
                    break;
                } 
            }
            if (colorSourceInfo != null)
            {
                selectedGroup = sourceGroup;
                break;
            }
        }

        var colorFrameSource = CameraCapture.FrameSources[colorSourceInfo.Id];
        var preferredFormat = colorFrameSource.SupportedFormats.Where(format =>
        {
            return format.Subtype == MediaEncodingSubtypes.Argb32;

        }).FirstOrDefault();

        CameraFrameReader = await CameraCapture.CreateFrameReaderAsync(colorFrameSource);
        await CameraFrameReader.StartAsync();
    }

    public void StartPullCameraFrames()
    {   
        System.Diagnostics.Debug.WriteLine("StartPullCameraFrames");
        Task.Run(async () =>
        {   
            var ModelHelper = new ONNXModelHelper(UnityApp);
            System.Diagnostics.Debug.WriteLine("model inited");
            for (; ; ) // Forever = While the app runs
            {
                FramesCaptured++;
                await Task.Delay(PredictionFrequency);
                using (var frameReference = CameraFrameReader.TryAcquireLatestFrame())
                using (var videoFrame = frameReference?.VideoMediaFrame?.GetVideoFrame())
                {   
                    if (videoFrame == null)
                    {   
                        System.Diagnostics.Debug.WriteLine("frame is null");
                        continue; //ignoring frame
                    }
                    if (videoFrame.Direct3DSurface == null)
                    {   
                        System.Diagnostics.Debug.WriteLine("d3d surface is null");
                        videoFrame.Dispose();
                        continue; //ignoring frame
                    }
                    try
                    {   
                        System.Diagnostics.Debug.WriteLine("trying to evaluate");
                        SpatialCoordinateSystem worldCoordinateSystem = m_referenceFrame.CoordinateSystem;
                        Matrix4x4 cameraToWorld = (Matrix4x4)frameReference.CoordinateSystem.TryGetTransformTo(worldCoordinateSystem);
                        CameraIntrinsics cameraIntrinsics = frameReference.VideoMediaFrame.CameraIntrinsics;
                        DepthMediaFrame depthFrame = frameReference.VideoMediaFrame.DepthMediaFrame;

                        await ModelHelper.EvaluateVideoFrameAsync(videoFrame, frameReference.VideoMediaFrame, worldCoordinateSystem, frameReference.CoordinateSystem).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                    finally
                    {
                    }
                }

            }

        });
    }

}
#endif