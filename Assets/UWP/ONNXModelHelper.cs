#if UNITY_WSA && !UNITY_EDITOR
using System;
using System.Windows;
using System.Numerics;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation.Numerics;
using Windows.Media;
using Windows.Foundation;
using Windows.Storage;
using Windows.Media.Devices.Core;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Perception.Spatial;
using Windows.Graphics.DirectX.Direct3D11;

public class ONNXModelHelper
{
    private ONNXModel Model = null;
    private string ModelFilename = "ONNXModel.onnx";
    private Stopwatch TimeRecorder = new Stopwatch();
    private IUnityScanScene UnityApp;

    

    public ONNXModelHelper()
    {
        UnityApp = null;
    }

    public ONNXModelHelper(IUnityScanScene unityApp)
    {
        UnityApp = unityApp;
        Task something = LoadModelAsync();
        something.Wait();
    }

    public async Task LoadModelAsync() // <-- 1
    {
        ModifyText($"Loading {ModelFilename}... Patience");

        try 
        {   
            System.Diagnostics.Debug.WriteLine("start loading model");
            TimeRecorder = Stopwatch.StartNew();

            var modelFile = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri($"ms-appx:///Data/StreamingAssets/{ModelFilename}"));
            Model = await ONNXModel.CreateOnnxModel(modelFile);

            TimeRecorder.Stop();
            System.Diagnostics.Debug.WriteLine("model loaded");
            ModifyText($"Loaded {ModelFilename}: Elapsed time: {TimeRecorder.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            ModifyText($"error: {ex.Message}");
            Model = null;
        }
    }

    public async Task EvaluateVideoFrameAsync(VideoFrame frame, VideoMediaFrame VideoFrame, SpatialCoordinateSystem worldCoordinateSystem, SpatialCoordinateSystem cameraCoordinateSystem) // <-- 2
    {
        if (frame != null)
        {
            try
            {   
                TimeRecorder.Restart();

                // A matrix to transform camera coordinate system to world coordinate system
                Matrix4x4 cameraToWorld = (Matrix4x4)cameraCoordinateSystem.TryGetTransformTo(worldCoordinateSystem);

                // Internal orientation of camera
                CameraIntrinsics cameraIntrinsics = VideoFrame.CameraIntrinsics;

                // The frame of depth camera
                DepthMediaFrame depthFrame = VideoFrame.DepthMediaFrame;
                
                // not working, cause error
                // DepthCorrelatedCoordinateMapper depthFrameMapper = depthFrame.TryCreateCoordinateMapper(cameraIntrinsics, cameraCoordinateSystem);

                ONNXModelInput inputData = new ONNXModelInput();
                inputData.Data = frame;
                var output = await Model.EvaluateAsync(inputData).ConfigureAwait(false); // <-- 3
                
                TimeRecorder.Stop();

                string timeStamp = $"({DateTime.Now})";
                // $" Evaluation took {TimeRecorder.ElapsedMilliseconds}ms\n";

                int count = 0;

                foreach (var prediction in output)
                {   
                    var product = prediction.TagName; // <-- 4 
                    var loss = prediction.Probability; // <-- 5

                    if (loss > 0.5f)
                    {   
                        float left = prediction.BoundingBox.Left;
                        float top = prediction.BoundingBox.Top; 
                        float right = prediction.BoundingBox.Left + prediction.BoundingBox.Width;
                        float bottom = prediction.BoundingBox.Top + prediction.BoundingBox.Height;
                        float x = prediction.BoundingBox.Left + prediction.BoundingBox.Width/2;
                        float y = prediction.BoundingBox.Top + prediction.BoundingBox.Height/2;

                        Direct3DSurfaceDescription pixelData = frame.Direct3DSurface.Description;	
                        int height = pixelData.Height;
                        int width = pixelData.Width;
                        
                        Vector3 ImageToWorld(float X, float Y)
                        {
                            // remove image distortion
                            // Point objectCenterPoint = cameraIntrinsics.UndistortPoint(new Point(x, y));
                            // screen space -> camera space 
                            // unproject pixel coordinate of object center towards a plane that is one meter from the camera
                            Vector2 objectCenter = cameraIntrinsics.UnprojectAtUnitDepth(new Point(X*width, Y*height));

                            // construct a ray towards object
                            Vector3 vectorTowardsObject = Vector3.Normalize(new Vector3(objectCenter.X, objectCenter.Y, -1.0f));

                            // estimate the vending machine distance by its width
                            // less accurate than use depth frame
                            // magic number 940 pixels in width for an average vending machine at 2m
                            // float estimatedVendingMachineDepth = (0.94f / prediction.BoundingBox.Width) * 2;
                            float estimatedVendingMachineDepth = (0.3f / prediction.BoundingBox.Width) * 1;

                            // times the vector towards object by the distance to get object's vector in camera coordinate system
                            Vector3 vectorToObject = vectorTowardsObject * estimatedVendingMachineDepth;

                            // camera space -> world space
                            // tranform the object postion from camera coordinate system to world coordinate system
                            Vector3 targetPositionInWorldSpace = Vector3.Transform(vectorToObject, cameraToWorld);

                            return targetPositionInWorldSpace;
                        }
                        
                        
                        Vector3 objectCenterInWorld = ImageToWorld(x,y);
                        Vector3 objectTopLeft = ImageToWorld(left, top);
                        Vector3 objectTopRight = ImageToWorld(right, top);
                        Vector3 objectBotLeft = ImageToWorld(left, bottom);
                        float widthInWorld = Vector3.Distance(objectTopLeft, objectTopRight);
                        float heightInWorld = widthInWorld/(width*prediction.BoundingBox.Width)*(height*prediction.BoundingBox.Height);
                        var lossStr = (loss * 100.0f).ToString("#0.00") + "%";
                        // lossStr = $"{prediction.BoundingBox.Width*width}X{prediction.BoundingBox.Height*height}";
                        UnityApp.StoreNetworkResult(timeStamp, product, lossStr, objectCenterInWorld.X, objectCenterInWorld.Y, objectCenterInWorld.Z, widthInWorld, heightInWorld);

                    }

                }
                
            }
            catch (Exception ex)
            {
                var err_message = $"{ex.Message}";
                ModifyText(err_message);
            }
        }
    }

    private void ModifyText(string text)
    {
        System.Diagnostics.Debug.WriteLine(text);
        if (UnityApp != null)
        {
            UnityApp.AddToMessageFlow(text);
        }
    }
}
#endif