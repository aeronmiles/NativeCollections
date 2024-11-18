using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class VideoCapPlugin : MonoBehaviour
{
  [SerializeField] private int _width = 1920;
  [SerializeField] private int _height = 1080;
  [SerializeField] private int _fps = 50;

#if UNITY_LINUX
  [DllImport("libNativeCollections")]
  private static extern void StartCapture(int width, int height, int fps);

  [DllImport("libNativeCollections")]
  private static extern void GetNextFrame(IntPtr pixelBuffer, int width, int height);

  [DllImport("libNativeCollections")]
  private static extern void StopCapture();

  [SerializeField] private Texture2D _texture;
  private GCHandle _pixelHandle;
  private Color32[] _pixelBuffer;

  private void Start()
  {
    _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
    _texture.name = "VideoCapPlugin::Start::_texture";
    _pixelBuffer = new Color32[_width * _height];
    _pixelHandle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);

    var webCamTexture = new WebCamTexture(WebCamTexture.devices[0].name, _width, _height, _fps);
    webCamTexture.Play();
    webCamTexture.Stop();
  }

  private bool _started = false;

  private void Update()
  {
    if (Input.GetKeyDown(KeyCode.PageUp))
    {
      StartCapture(_texture.width, _texture.height, _fps);
      _started = true;
    }
    if (!_started)
    {
      return;
    }
    IntPtr pixelPtr = _pixelHandle.AddrOfPinnedObject();
    GetNextFrame(pixelPtr, _texture.width, _texture.height);
    // _texture.SetPixels32(_pixelBuffer);
    _texture.Apply();
  }

  private void OnDestroy()
  {
    StopCapture();
    if (_pixelHandle.IsAllocated)
    {
      _pixelHandle.Free();
    }
    _texture = null;
    _pixelBuffer = null;
  }

  public Texture2D GetTexture() => _texture;
#endif
}
