using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativePlugin : MonoBehaviour
{
  [DllImport("NativePlugin")]
  private static extern void StartCapture(int width, int height, int fps);

  [DllImport("NativePlugin")]
  private static extern void GetNextFrame(IntPtr pixelBuffer, int width, int height);

  [DllImport("NativePlugin")]
  private static extern void StopCapture();

  private Texture2D texture;
  private GCHandle pixelHandle;
  private Color32[] pixelBuffer;

  void Start()
  {
    int width = 1280;
    int height = 720;
    int fps = 30;

    texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
    pixelBuffer = new Color32[width * height];
    pixelHandle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);

    StartCapture(width, height, fps);
  }

  void Update()
  {
    IntPtr pixelPtr = pixelHandle.AddrOfPinnedObject();
    GetNextFrame(pixelPtr, texture.width, texture.height);
    texture.SetPixels32(pixelBuffer);
    texture.Apply();
  }

  void OnDestroy()
  {
    StopCapture();
    if (pixelHandle.IsAllocated)
    {
      pixelHandle.Free();
    }
    texture = null;
    pixelBuffer = null;
  }

  public Texture2D GetTexture()
  {
    return texture;
  }
}
