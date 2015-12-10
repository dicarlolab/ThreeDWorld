using UnityEngine;
using System.Collections;
using NetMQ;
using System;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class CameraStreamer : MonoBehaviour
{
#region Data Structures
    public class CapturedImage
    {
        public byte[] pictureBuffer;
    }
    public delegate void CollectImages(CaptureRequest completedRequest);
    public class CaptureRequest
    {
        public CollectImages callbackFunc = null;
        public List<Shader> shadersList = null;
        public List<CapturedImage> capturedImages = null;
    }
#endregion

#region Fields
    public Camera targetCam = null;
    public RawImage testImage = null;

    private Camera textureCam = null;
    private Texture2D outPhoto = null;
    private Queue<CaptureRequest> captureRequests = new Queue<CaptureRequest>();
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
//        if (captureRequests.Count > 0)
//            Debug.LogFormat("Awaiting PostRender for {0} captures", captureRequests.Count);
    }

//    // Called after the camera renders
//    void OnPostRender()
//    {
//        // Note: It seems that Input.GetKeyDown doesn't work herein OnPostRender
//        while (captureRequests.Count > 0)
//            ProcessCaptureRequest(captureRequests.Dequeue());
//    }
#endregion

#region Public functions
//    public void RequestCaptures(List<Shader> shadersToUse = null, CollectImages callbackFunc = null)
//    {
//        CaptureRequest newRequest = new CaptureRequest();
//        newRequest.callbackFunc = callbackFunc;
//        newRequest.shadersList = shadersToUse;
//        newRequest.capturedImages = null;
//        RequestCaptures(newRequest);
//    }
    
    public void RequestCaptures(CaptureRequest newRequest)
    {
        if (captureRequests.Contains(newRequest))
        {
            Debug.Log("Already have a pending request");
            return;
        }
        if (newRequest.shadersList == null)
        {
            newRequest.shadersList = new List<Shader>();
            newRequest.shadersList.Add(null);
        }
        captureRequests.Enqueue(newRequest);
        StartCoroutine(CaptureCoroutine());
    }

    public IEnumerator CaptureCoroutine()
    {
        yield return new WaitForEndOfFrame();
        while(captureRequests.Count > 0)
            ProcessCaptureRequest(captureRequests.Dequeue());
    }

    public static void SaveOutImages(CaptureRequest completedRequest)
    {
        int numValues = Mathf.Min(completedRequest.shadersList.Count, completedRequest.capturedImages.Count);
        for(int i = 0; i < numValues; ++i)
            SaveOutImages(completedRequest.capturedImages[i].pictureBuffer, i);
        fileIndex++;
    }
    
    public static void SaveOutImages(byte[] imageData, int shaderIndex)
    {
        string newFileName = string.Format("{0}/{1}{2}_shader{3}.png", Application.persistentDataPath, fileName, fileIndex, shaderIndex);
        System.IO.File.WriteAllBytes(newFileName, imageData);
//        Debug.Log("Trying to saved image at " + newFileName);
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
            Debug.Log("Capture count now: " + request.capturedImages.Count);
        }
        for(int i = 0; i < request.shadersList.Count; ++i)
            request.capturedImages[i].pictureBuffer = TakeSnapshotNow(request.shadersList[i]).pictureBuffer;
        if (request.callbackFunc != null)
            request.callbackFunc(request);
    }

    private CapturedImage TakeSnapshotNow(Shader targetShader)
    {
        if (textureCam == null)
        {
            GameObject newObj = new GameObject("Texture-Writing Camera");
            textureCam = newObj.AddComponent<Camera>();
            textureCam.enabled = false;
            textureCam.targetTexture = new RenderTexture(targetCam.pixelWidth, targetCam.pixelHeight, 0);
            if (testImage != null)
                testImage.texture = textureCam.targetTexture;
        }
        if (targetShader != null)
            targetCam.RenderWithShader(targetShader, null);
        else
            targetCam.Render();

        const bool SHOULD_USE_MIPMAPS = false;
        int pixWidth = textureCam.targetTexture.width;
        int pixHeight = textureCam.targetTexture.height;
        if (outPhoto == null || outPhoto.width != pixWidth || outPhoto.height != pixHeight)
            outPhoto = new Texture2D(pixWidth, pixHeight, TextureFormat.ARGB32, SHOULD_USE_MIPMAPS);
        outPhoto.ReadPixels(new Rect(0, 0, pixWidth, pixHeight), 0, 0);
        outPhoto.Apply();
        CapturedImage retImage = new CapturedImage();
        retImage.pictureBuffer = outPhoto.EncodeToPNG();
        return retImage;
    }
#endregion
}
