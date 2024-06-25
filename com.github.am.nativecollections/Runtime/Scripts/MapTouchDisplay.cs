using System;
using System.Text.RegularExpressions;
using UnityEngine;

public class MapTouchDisplay : MonoBehaviour
{
#if UNITY_STANDALONE_LINUX
  public static event Action<string> OnError;

  // @TODO: Configuration setup
  [SerializeField] private string _touchDisplayPort = "DP-1";
  [SerializeField] private string _touchDeviceName = "TSTP MTouch";

  // Call the functions to list devices, parse the ID, and map the device
  private void Start()
  {
    string xinputListOutput = ExecuteCommand("xinput list");
    int deviceId = GetDeviceId(xinputListOutput);
    if (deviceId != -1)
    {
      Debug.Log($"Mapping touch device with ID: {deviceId}.");

      // Get display and screen dimensions
      var (displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight) = GetDisplayDimensions(_touchDisplayPort);

      MapDeviceToOutput(deviceId, _touchDisplayPort);
      SetCoordinateTransformationMatrix(deviceId, displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight);
    }
  }

  private string ExecuteCommand(string command)
  {
    Debug.Log($"Executing command: bash -c {command}");
    var result = ExecuteCommandPlugin.RunCommand($"bash -c \"{command}\"");
    Debug.Log($"Command output: {result}");
    return result;
  }

  // Function to parse the device ID for touch device
  private int GetDeviceId(string xinputListOutput)
  {
    string pattern = $@"{_touchDeviceName}\s+id=(\d+)";
    Match match = Regex.Match(xinputListOutput, pattern);

    if (match.Success && int.TryParse(match.Groups[1].Value, out int deviceId))
    {
      return deviceId;
    }

    Debug.LogError($"{_touchDeviceName} device not found.");
    OnError?.Invoke($"{_touchDeviceName} device not found.");
    return -1;
  }

  // Function to map the device to the specified output
  private void MapDeviceToOutput(int deviceId, string output)
  {
    if (deviceId < 0)
    {
      return;
    }

    string command = $"xinput map-to-output {deviceId} {output}";
    _ = ExecuteCommand(command);
  }

  // Function to get the display dimensions using xrandr
  private (float displayWidth, float displayHeight, float displayX, float displayY, float totalWidth, float totalHeight) GetDisplayDimensions(string outputDisplay)
  {
    string xrandrOutput = ExecuteCommand("xrandr");
    float displayWidth = 1920f;
    float displayHeight = 1080f;
    float displayX = 0f;
    float displayY = 0f;
    float totalWidth = Screen.width;
    float totalHeight = Screen.height;

    // Regex to find the current resolution and position of the specified display
    string displayPattern = $@"{Regex.Escape(outputDisplay)} connected.*?(\d+)x(\d+)\+(\d+)\+(\d+)";
    Match displayMatch = Regex.Match(xrandrOutput, displayPattern);

    if (displayMatch.Success)
    {
      displayWidth = float.Parse(displayMatch.Groups[1].Value);
      displayHeight = float.Parse(displayMatch.Groups[2].Value);
      displayX = float.Parse(displayMatch.Groups[3].Value);
      displayY = float.Parse(displayMatch.Groups[4].Value);
    }

    string screenPattern = @"current (\d+) x (\d+)";
    Match screenMatch = Regex.Match(xrandrOutput, screenPattern);

    if (screenMatch.Success)
    {
      totalWidth = float.Parse(screenMatch.Groups[1].Value);
      totalHeight = float.Parse(screenMatch.Groups[2].Value);
    }

    Debug.Log($"Display: {displayWidth}x{displayHeight}+{displayX}+{displayY}, Screen: {totalWidth}x{totalHeight}");
    return (displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight);
  }

  // Function to set the coordinate transformation matrix
  private void SetCoordinateTransformationMatrix(int deviceId, float displayWidth, float displayHeight, float displayX, float displayY, float totalWidth, float totalHeight)
  {
    if (deviceId < 0)
    {
      return;
    }

    // Calculate the transformation matrix to map the touch to the specific display
    float scaleX = displayWidth / totalWidth;
    float scaleY = displayHeight / totalHeight;
    float offsetX = displayX / totalWidth;
    float offsetY = displayY / totalHeight;

    Debug.Log($"Setting transformation matrix: {scaleX} 0 {offsetX} 0 {scaleY} {offsetY} 0 0 1");
    string command = $"xinput set-prop {deviceId} 'Coordinate Transformation Matrix' {scaleX} 0 {offsetX} 0 {scaleY} {offsetY} 0 0 1";
    _ = ExecuteCommand(command);
  }
#endif
}
