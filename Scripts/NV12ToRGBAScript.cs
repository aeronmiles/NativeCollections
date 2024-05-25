using UnityEngine;
using System.Runtime.InteropServices;

public class NV12ToRGBAScript : MonoBehaviour
{
  [DllImport("YourNativePlugin")]
  private static extern void GetNextFrame(System.IntPtr pixelBuffer, int width, int height);

  public Material material;
  public int width = 3840;
  public int height = 2160;

  [SerializField] private Texture2D nv12Texture;
  private Color32[] nv12Pixels;
  private GCHandle nv12Handle;
  private System.IntPtr unv12Ptr;

  void Start()
  {
    nv12Texture = new Texture2D(width, height + height / 2, TextureFormat.Alpha8, false);
    nv12Pixels = new Color32[width * (height + height / 2)];
    nv12Handle = GCHandle.Alloc(nv12Pixels, GCHandleType.Pinned);
    nv12Ptr = nv12Handle.AddrOfPinnedObject();
  }

  void Update()
  {
    GetNextFrame(nv12Ptr, width, height);
    nv12Texture.LoadRawTextureData(nv12Ptr, width * (height + height / 2));
    nv12Texture.Apply();

    material?.SetTexture("_MainTex", nv12Texture);
  }

  void OnDestroy()
  {
    nv12Handle.Free();
  }
}