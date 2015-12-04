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
        public List<CapturedImage> retValue = null;
    }
#endregion

#region Fields
    public Camera targetCam = null;
    public RawImage testImage = null;

    private Camera textureCam = null;
    private Texture2D outPhoto = null;
    private Queue<CaptureRequest> captureRequests = new Queue<CaptureRequest>();
    private static int fileIndex = 0;
    const string fileName = "testImg";
#endregion

#region Unity callbacks
    // Use this for initialization
    void Start()
    {
    }

    // Called after the camera renders
    void OnPostRender()
    {
        // Note: It seems that Input.GetKeyDown doesn't work herein OnPostRender
        while (captureRequests.Count > 0)
            ProcessCaptureRequest(captureRequests.Dequeue());
    }
#endregion

#region Public functions
    public void RequestCaptures(List<Shader> shadersToUse = null, CollectImages callbackFunc = null)
    {
        CaptureRequest newRequest = new CaptureRequest();
        newRequest.callbackFunc = callbackFunc;
        newRequest.shadersList = shadersToUse;
        newRequest.retValue = null;
        RequestCaptures(newRequest);
    }
    
    public void RequestCaptures(CaptureRequest newRequest)
    {
        if (newRequest.shadersList == null)
        {
            newRequest.shadersList = new List<Shader>();
            newRequest.shadersList.Add(null);
        }
        captureRequests.Enqueue(newRequest);
    }

    public static void SaveOutImages(CaptureRequest completedRequest)
    {
        string newFileName = "";
        int numValues = Mathf.Min(completedRequest.shadersList.Count, completedRequest.retValue.Count);
        for(int i = 0; i < numValues; ++i)
        {
            newFileName = string.Format("{0}/{1}{2}_shader{3}.png", Application.persistentDataPath, fileName, fileIndex, i);
            System.IO.File.WriteAllBytes(newFileName, completedRequest.retValue[i].pictureBuffer);
            Debug.Log("Trying to saved image at " + newFileName);
        }
        fileIndex++;
    }
#endregion

#region Internal functions
    private void ProcessCaptureRequest(CaptureRequest request)
    {
        Debug.Log("ProcessCaptureRequest() called");
        if (request.retValue == null)
            request.retValue = new List<CapturedImage>();
        while(request.retValue.Count < request.shadersList.Count)
            request.retValue.Add(new CapturedImage());
        for(int i = 0; i < request.shadersList.Count; ++i)
            request.retValue[i].pictureBuffer = TakeSnapshotNow(request.shadersList[i]).pictureBuffer;
        if (request.callbackFunc != null)
            request.callbackFunc(request);
    }

    private CapturedImage TakeSnapshotNow(Shader targetShader)
    {
        Debug.Log("Taking snapshot!");
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
        outPhoto = new Texture2D(pixWidth, pixHeight, TextureFormat.ARGB32, SHOULD_USE_MIPMAPS);
        outPhoto.ReadPixels(new Rect(0, 0, pixWidth, pixHeight), 0, 0);
        outPhoto.Apply();
        CapturedImage retImage = new CapturedImage();
        retImage.pictureBuffer = outPhoto.EncodeToPNG();
        return retImage;
    }
#endregion
}
