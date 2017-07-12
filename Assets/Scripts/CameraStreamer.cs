using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityStandardAssets.CinematicEffects;

/// <summary>
/// Class that captures render output from a camera and returns a 
/// callback with the image data
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraStreamer : MonoBehaviour
{
    bool DEBUG = false;
#region Data Structures
    // Simple class to make it more clear when passing picture image data
    public class CapturedImage
    {
        public byte[] pictureBuffer;
    }
    public delegate void CollectImages(CaptureRequest completedRequest);

    // Data structure for requesting an image capture from
    // this camera with the given shaders and callbacks.
    // Output is added to capturedImages, overriding any previous data
    public class CaptureRequest
    {
        public CollectImages callbackFunc = null;
        public List<Shader> shadersList = null;
        public List<string> outputFormatList = null;
        public List<CapturedImage> capturedImages = null;
    }
#endregion

#region Fields
    public List<Camera> targetCams;
    public List<Camera> shaderCams;
    // Debug UI image that shows what this camera is rendering out
    public RawImage testImage = null;

    private Camera _textureCam = null;
    private Texture2D _outPhoto = null;
    // If not empty, this queue will have the camera will capture a render and send a callback
    private Queue<CaptureRequest> _captureRequests = new Queue<CaptureRequest>();
    public static string preferredImageFormat = "bmp";
    public static int fileIndex = 0;
    const string fileName = "testImg";

    // BMP Header caching info
    private static byte[] _bmpHeader = null;
    private static UInt32 _lastBmpDimX = 0;
    private static UInt32 _lastBmpDimY = 0;

    // Snapshot init
    private List<bool> snapshotInit;
#endregion

#region Unity callbacks
    // Use this for initialization
    void Start()
    {
    	snapshotInit = new List<bool>();
    	for(int i = 0; i < targetCams.Count; i++)
    	{
    	   	snapshotInit.Add(true);
    	}
    }

    void Update()
    {
    }
#endregion

#region Public functions
    public void RequestCaptures(CaptureRequest newRequest)
    {
        if (_captureRequests.Contains(newRequest))
        {
            if(DEBUG)
                Debug.Log("Already have a pending request");
            return;
        }
        if (newRequest.shadersList == null)
        {
            newRequest.shadersList = new List<Shader>();
            newRequest.shadersList.Add(null);
        }
        _captureRequests.Enqueue(newRequest);
        StartCoroutine(CaptureCoroutine());
    }

    // Coroutine to run through all the render requests on this camera
    // at the end of the frame(to avoid conflicts with the main rendering logic)
    public IEnumerator CaptureCoroutine()
    {
        if (DEBUG && NetMessenger.logTimingInfo)
            Debug.LogFormat("Start CaptureCoroutine() {0}", Utils.GetTimeStamp());
        yield return new WaitForEndOfFrame();
        if (DEBUG && NetMessenger.logTimingInfo)
            Debug.LogFormat("Reached end of frame {0}", Utils.GetTimeStamp());
        while(_captureRequests.Count > 0)
            ProcessCaptureRequest(_captureRequests.Dequeue());
    }

    // Function to save out captured image data to disk as png files(mainly for debugging)
    public static void SaveOutImages(CaptureRequest completedRequest)
    {
        int numValues = Mathf.Min(completedRequest.shadersList.Count, completedRequest.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            SaveOutImages(completedRequest.capturedImages[i].pictureBuffer, i);
        fileIndex++;
    }
    
    // Function to save out raw image data to disk as png files(mainly for debugging)
    public static string SaveOutImages(byte[] imageData, int shaderIndex)
    {
        string newFileName = string.Format("{0}/{1}{2}_shader{3}.{4}", Application.persistentDataPath, fileName, fileIndex, shaderIndex, preferredImageFormat);
        System.IO.File.WriteAllBytes(newFileName, imageData);
        return newFileName;
    }
#endregion

#region Internal functions
    private void ProcessCaptureRequest(CaptureRequest request)
    {
        if (request.capturedImages == null)
            request.capturedImages = new List<CapturedImage>();
        // times 2 for second camera
		while(request.capturedImages.Count < request.shadersList.Count * targetCams.Count)
        {
			request.capturedImages.Add(new CapturedImage());
//            Debug.Log("Capture count now: " + request.capturedImages.Count);
        }
		UpdateObjectTransforms(targetCams);
        for (int c = 0; c < targetCams.Count; c++)
        {
        	// Choose the appropriate previous transforms for velocity calculation
			SelectObjectTransforms(c);
        	//Render and write images
        	for (int i = 0; i < request.shadersList.Count; ++i)
        	{
            	if(DEBUG)
            	{
                	Debug.Log("SHADER CALL");
                	Debug.Log(i);
            	}
				request.capturedImages[i+c*request.shadersList.Count].pictureBuffer = TakeSnapshotNow(request.shadersList[i], c, request.outputFormatList[i]).pictureBuffer;
			}
        }
        if (request.callbackFunc != null)
            request.callbackFunc(request);
    }

    public void ResetObjectTransforms(int n_cameras)
    {
		SemanticObject[] allObjects = UnityEngine.Object.FindObjectsOfType<SemanticObject>();
		foreach(SemanticObject obj in allObjects)
		{
			Renderer[] rendererList = obj.GetComponentsInChildren<Renderer>();
			foreach (Renderer _rend in rendererList)
            {
				MaterialPropertyBlock properties = new MaterialPropertyBlock();
				_rend.GetPropertyBlock(properties);
				for(int t = 0; t < 4; t++) 
            	{
					properties.SetMatrixArray(String.Format("_{0}MVPs", t), Utils.initTransforms(n_cameras));
					properties.SetMatrixArray(String.Format("_{0}MVs", t), Utils.initTransforms(n_cameras));
				}
            	_rend.SetPropertyBlock(properties);
            }
		}
    }

    private void SelectObjectTransforms(int camera_id)
    {
		SemanticObject[] allObjects = UnityEngine.Object.FindObjectsOfType<SemanticObject>();
		foreach(SemanticObject obj in allObjects)
		{
			Renderer[] rendererList = obj.GetComponentsInChildren<Renderer>();
			foreach (Renderer _rend in rendererList)
            {
				MaterialPropertyBlock properties = new MaterialPropertyBlock();
				_rend.GetPropertyBlock(properties);
            	foreach (Material _mat in _rend.materials)
                {
					for(int t = 0; t < 4; t++) 
            		{
						_mat.SetMatrix(String.Format("_{0}MVP", t), 
							properties.GetMatrixArray(String.Format("_{0}MVPs", t))[camera_id]);
						_mat.SetMatrix(String.Format("_{0}MV", t), 
							properties.GetMatrixArray(String.Format("_{0}MVs", t))[camera_id]);
					}
				}
            }
		}
    }

    private void UpdateObjectTransforms(List<Camera> cams)
    {
		SemanticObject[] allObjects = UnityEngine.Object.FindObjectsOfType<SemanticObject>();
		foreach(SemanticObject obj in allObjects)
		{
			Renderer[] rendererList = obj.GetComponentsInChildren<Renderer>();
			foreach (Renderer _rend in rendererList)
            {
				MaterialPropertyBlock properties = new MaterialPropertyBlock();
				_rend.GetPropertyBlock(properties);
				List<Matrix4x4[]> MVPs = new List<Matrix4x4[]>();
				List<Matrix4x4[]> MVs = new List<Matrix4x4[]>();
				for(int t = 0; t < 4; t++) 
            	{
					Matrix4x4[] MVP = properties.GetMatrixArray(String.Format("_{0}MVPs", t));
					if(MVP == null)
						MVP = Utils.initTransforms(cams.Count);
					MVPs.Add(MVP);
					Matrix4x4[] MV = properties.GetMatrixArray(String.Format("_{0}MVs", t));
					if(MV == null)
						MV = Utils.initTransforms(cams.Count);
					MVs.Add(MV);
				}
				Debug.Assert(MVs[0].Length == cams.Count);
				Debug.Assert(MVPs[0].Length == cams.Count);

				for(int t = 2; t >= 0; t--) 
            	{
					Array.Copy(MVs[t], MVs[t+1], MVs[t].Length);
					properties.SetMatrixArray(String.Format("_{0}MVs", t+1), MVs[t+1]);
					Array.Copy(MVPs[t], MVPs[t+1], MVPs[t].Length);
					properties.SetMatrixArray(String.Format("_{0}MVPs", t+1), MVPs[t+1]);
				}

				for(int c = 0; c < cams.Count; c++)
				{
					// Get projection and camera transformation matrix
					Matrix4x4 P = GL.GetGPUProjectionMatrix(cams[c].projectionMatrix, false);
					Matrix4x4 V = cams[c].worldToCameraMatrix;
            		// Get model tranformation matrix
					Matrix4x4 M = _rend.localToWorldMatrix;
					Matrix4x4 MV = V * M;
					Matrix4x4 MVP = P * V * M;

					MVs[0][c] = MV;
					MVPs[0][c] = MVP;
				}
				properties.SetMatrixArray("_0MVs", MVs[0]);
				properties.SetMatrixArray("_0MVPs", MVPs[0]);
            	_rend.SetPropertyBlock(properties);
            }
		}
    }

    private void OnDisable()
    {
        if (_textureCam != null && _textureCam.gameObject != null)
            GameObject.Destroy(_textureCam.gameObject);
        _textureCam = null;
    }

    private static void UpdateBMPHeader(UInt32 dimX, UInt32 dimY)
    {
        if (dimX != _lastBmpDimX || dimY != _lastBmpDimY || _bmpHeader == null)
        {
            const UInt32 HEADER_SIZE = 70;
            UInt32 size = dimX * dimY, hSze = size + HEADER_SIZE;
            _bmpHeader = new byte[]{
                0x42, 0x4D,
                BitConverter.GetBytes(hSze)[0], BitConverter.GetBytes(hSze)[1], BitConverter.GetBytes(hSze)[2], BitConverter.GetBytes(hSze)[3], // Size + header length(70)
                0x00, 0x00, 0x00, 0x00,
                0x46, 0x00, 0x00, 0x00,
                0x38, 0x00, 0x00, 0x00,
                BitConverter.GetBytes(dimX)[0], BitConverter.GetBytes(dimX)[1], BitConverter.GetBytes(dimX)[2], BitConverter.GetBytes(dimX)[3], // Width
                BitConverter.GetBytes(dimY)[0], BitConverter.GetBytes(dimY)[1], BitConverter.GetBytes(dimY)[2], BitConverter.GetBytes(dimY)[3], // Height
                0x01, 0x00, 0x20, 0x00,
                0x03, 0x00, 0x00, 0x00,
                BitConverter.GetBytes(size)[0], BitConverter.GetBytes(size)[1], BitConverter.GetBytes(size)[2], BitConverter.GetBytes(size)[3], // Size
                0x13, 0x0B, 0x00, 0x00,
                0x13, 0x0B, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0xFF,
                0x00, 0x00, 0xFF, 0x00,
                0x00, 0xFF, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };
            _lastBmpDimX = dimX;
            _lastBmpDimY = dimY;
        }
    }

    private static void EncodeBMP(ref CapturedImage imgData, Texture2D textureSrc, int width, int height)
    {
        // Write out BMP file
        UpdateBMPHeader((UInt32)width, (UInt32)height);
        int byteArrayLength = _bmpHeader.Length + width * height * 4;
        Color32[] pixels = textureSrc.GetPixels32();
        if (imgData.pictureBuffer == null || imgData.pictureBuffer.Length != byteArrayLength)
            imgData.pictureBuffer = new byte[byteArrayLength];
        int byteIndex = 0;
        System.Buffer.BlockCopy(_bmpHeader, 0, imgData.pictureBuffer, 0, _bmpHeader.Length);
        for(int i = 0; i < pixels.Length; ++i)
        {
            byteIndex = 4*i + _bmpHeader.Length;
            imgData.pictureBuffer[byteIndex+0] = pixels[i].a;
            imgData.pictureBuffer[byteIndex+1] = pixels[i].b;
            imgData.pictureBuffer[byteIndex+2] = pixels[i].g;
            imgData.pictureBuffer[byteIndex+3] = pixels[i].r;
        }        
    }


    private CapturedImage TakeSnapshotNow(Shader targetShader, int camera_id, string outputFormat = "use_preferred_output_format")
    {
        if(DEBUG)
        {
            Debug.Log("NAME SHADER");
            if (targetShader != null)
            { Debug.Log(targetShader.name); }
        }


        if (DEBUG && NetMessenger.logTimingInfo)
            Debug.LogFormat("Start TakeShapshotNow() {0} {1}", (targetShader == null) ? "(null)" : targetShader.name, Utils.GetTimeStamp());
        // Create a new camera if we need to that we will be manually rendering
        if (_textureCam == null)
        {
            if(DEBUG)
                Debug.Log ("Texture cam is null");

            GameObject newObj = new GameObject("Texture-Writing Camera");
            _textureCam = newObj.AddComponent<Camera>();
            _textureCam.enabled = false;
            _textureCam.targetTexture = new RenderTexture(targetCams[camera_id].pixelWidth, targetCams[camera_id].pixelHeight, 0, RenderTextureFormat.ARGB32);

            if (testImage != null)
                testImage.texture = _textureCam.targetTexture;
         }
         if(snapshotInit[camera_id])
         {
			snapshotInit[camera_id] = false;
            // Image Effects
			if (true && targetCams[camera_id] != null)
            {
				targetCams[camera_id].hdr = true;

	            // Tone Mapping
				targetCams[camera_id].gameObject.AddComponent<TonemappingColorGrading>();
				var tonemapping = targetCams[camera_id].gameObject.GetComponent<TonemappingColorGrading>().tonemapping; //ToneMappingSettings
	            tonemapping.enabled = true;
	            tonemapping.exposure = 2;
	            tonemapping.tonemapper = TonemappingColorGrading.Tonemapper.Photographic;
				targetCams[camera_id].gameObject.GetComponent<TonemappingColorGrading>().tonemapping = tonemapping;

	            // Eye Adaptation
				var eyeadaptation = targetCams[camera_id].gameObject.GetComponent<TonemappingColorGrading>().eyeAdaptation; //EyeAdaptationSettings
	                eyeadaptation.enabled = true;
				targetCams[camera_id].gameObject.GetComponent<TonemappingColorGrading>().eyeAdaptation = eyeadaptation;

	            // Depth of Field
				targetCams[camera_id].gameObject.AddComponent<DepthOfField>();

	            //Ambient Occlusion
				targetCams[camera_id].gameObject.AddComponent<AmbientOcclusion>();

	            //Screen Space Reflections
				//targetCams[camera_id].renderingPath = RenderingPath.DeferredShading;
				//targetCams[camera_id].gameObject.AddComponent<ScreenSpaceReflection>();
	        }
        }

        // Call render with the appropriate shaders
        if (targetShader != null)
        {
			RenderTexture.active = shaderCams[camera_id].targetTexture;
			shaderCams[camera_id].RenderWithShader(targetShader, null);
        }
        else
        {
			RenderTexture.active = targetCams[camera_id].targetTexture;
			targetCams[camera_id].Render();
        }
        if (DEBUG && NetMessenger.logTimingInfo)
            Debug.LogFormat("  Finished Rendering {0}", Utils.GetTimeStamp());

        // Copy and convert rendered image to PNG format as a byte array
        const bool SHOULD_USE_MIPMAPS = false;
        int pixWidth = _textureCam.targetTexture.width;
        int pixHeight = _textureCam.targetTexture.height;

        if (_outPhoto == null || _outPhoto.width != pixWidth || _outPhoto.height != pixHeight) 
            _outPhoto = new Texture2D(pixWidth, pixHeight, TextureFormat.ARGB32, SHOULD_USE_MIPMAPS);

        _outPhoto.ReadPixels(new Rect(0, 0, pixWidth, pixHeight), 0, 0);
        _outPhoto.Apply();

        if (DEBUG && NetMessenger.logTimingInfo)
            Debug.LogFormat("  Created texture(internal format) {0}", Utils.GetTimeStamp());

        CapturedImage retImage = new CapturedImage();

	if(outputFormat == "use_preferred_output_format") 
        {
        	if (preferredImageFormat == "png")
            	retImage.pictureBuffer = _outPhoto.EncodeToPNG();
        	else if (preferredImageFormat == "jpg")
            	retImage.pictureBuffer = _outPhoto.EncodeToJPG();
        	else
            	EncodeBMP(ref retImage, _outPhoto, pixWidth, pixHeight);
        }
        else if (outputFormat == "png")
			retImage.pictureBuffer = _outPhoto.EncodeToPNG();
		else if (outputFormat == "jpg")
			retImage.pictureBuffer = _outPhoto.EncodeToJPG();
		else
			EncodeBMP(ref retImage, _outPhoto, pixWidth, pixHeight);

        if (NetMessenger.logTimingInfo)
            Debug.LogFormat("  Encoded image {0}", Utils.GetTimeStamp());

        return retImage;
    }
#endregion
}
