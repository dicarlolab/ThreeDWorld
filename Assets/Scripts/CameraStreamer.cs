using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Class that captures render output from a camera and returns a 
/// callback with the image data
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraStreamer : MonoBehaviour
{
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
        public List<CapturedImage> capturedImages = null;
    }
#endregion

#region Fields
    public Camera targetCam = null;
    // Debug UI image that shows what this camera is rendering out
    public RawImage testImage = null;

    private Camera _textureCam = null;
    private Texture2D _outPhoto = null;
    // If not empty, this queue will have the camera will capture a render and send a callback
    private Queue<CaptureRequest> _captureRequests = new Queue<CaptureRequest>();
    public static string preferredImageFormat = "bmp";
    public static int fileIndex = 0;
    const string fileName = "testImg";
#endregion

#region Unity callbacks
    // Use this for initialization
    void Start()
    {
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
        yield return new WaitForEndOfFrame();
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
        while(request.capturedImages.Count < request.shadersList.Count)
        {
            request.capturedImages.Add(new CapturedImage());
//            Debug.Log("Capture count now: " + request.capturedImages.Count);
        }
        for(int i = 0; i < request.shadersList.Count; ++i)
            request.capturedImages[i].pictureBuffer = TakeSnapshotNow(request.shadersList[i]).pictureBuffer;
        if (request.callbackFunc != null)
            request.callbackFunc(request);
    }

    private void OnDisable()
    {
        if (_textureCam != null && _textureCam.gameObject != null)
            GameObject.Destroy(_textureCam.gameObject);
        _textureCam = null;
    }

    private static byte[] CreateBMPHeader(UInt32 dimX, UInt32 dimY)
    {
        const UInt32 HEADER_SIZE = 70;
        UInt32 size = dimX * dimY, hSze = size + HEADER_SIZE;

        return new byte[]{
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
    }

    private static void EncodeBMP(ref CapturedImage imgData, Texture2D textureSrc, int width, int height)
    {
        // Write out BMP file
        byte [] header = CreateBMPHeader((UInt32)width, (UInt32)height);
        int byteArrayLength = header.Length + width * height * 4;
        Color32[] pixels = textureSrc.GetPixels32();
        if (imgData.pictureBuffer == null || imgData.pictureBuffer.Length != byteArrayLength)
            imgData.pictureBuffer = new byte[byteArrayLength];
        int byteIndex = 0;
        System.Buffer.BlockCopy(header, 0, imgData.pictureBuffer, 0, header.Length);
        for(int i = 0; i < pixels.Length; ++i)
        {
            byteIndex = 4*i + header.Length;
            imgData.pictureBuffer[byteIndex+0] = pixels[i].a;
            imgData.pictureBuffer[byteIndex+1] = pixels[i].b;
            imgData.pictureBuffer[byteIndex+2] = pixels[i].g;
            imgData.pictureBuffer[byteIndex+3] = pixels[i].r;
        }        
    }

    private CapturedImage TakeSnapshotNow(Shader targetShader)
    {
        // Create a new camera if we need to that we will be manually rendering
        if (_textureCam == null)
        {
            GameObject newObj = new GameObject("Texture-Writing Camera");
            _textureCam = newObj.AddComponent<Camera>();
            _textureCam.enabled = false;
            _textureCam.targetTexture = new RenderTexture(targetCam.pixelWidth, targetCam.pixelHeight, 0, RenderTextureFormat.ARGB32);
            if (testImage != null)
                testImage.texture = _textureCam.targetTexture;
        }

        // Call render with the appropriate shaders
        if (targetShader != null)
            targetCam.RenderWithShader(targetShader, null);
        else
            targetCam.Render();

        // Copy and convert rendered image to PNG format as a byte array
        const bool SHOULD_USE_MIPMAPS = false;
        int pixWidth = _textureCam.targetTexture.width;
        int pixHeight = _textureCam.targetTexture.height;
        if (_outPhoto == null || _outPhoto.width != pixWidth || _outPhoto.height != pixHeight)
            _outPhoto = new Texture2D(pixWidth, pixHeight, TextureFormat.ARGB32, SHOULD_USE_MIPMAPS);
        _outPhoto.ReadPixels(new Rect(0, 0, pixWidth, pixHeight), 0, 0);
        _outPhoto.Apply();
        CapturedImage retImage = new CapturedImage();
        if (preferredImageFormat == "png")
            retImage.pictureBuffer = _outPhoto.EncodeToPNG();
        else if (preferredImageFormat == "jpg")
            retImage.pictureBuffer = _outPhoto.EncodeToJPG();
        else
            EncodeBMP(ref retImage, _outPhoto, pixWidth, pixHeight);
        return retImage;
    }
#endregion
}
