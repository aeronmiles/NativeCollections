using System;
using System.Runtime.InteropServices;

public class ExecuteCommandPlugin
{
#if UNITY_STANDALONE_LINUX
  // Import the ExecuteCommand function from the shared library
  [DllImport("libNativeCollections")]
  private static extern IntPtr ExecuteCommand(string command);

  // Wrapper function to call the native plugin and get the result as a string
  public static string RunCommand(string command)
  {
    IntPtr resultPtr = ExecuteCommand(command);
    string result = Marshal.PtrToStringAnsi(resultPtr);
    return result;
  }
#endif
}
