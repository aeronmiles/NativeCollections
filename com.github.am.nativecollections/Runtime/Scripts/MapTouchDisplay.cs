using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class MapTouchDisplay : MonoBehaviour
{
#if UNITY_STANDALONE_LINUX
  public static event Action<string> OnError;

  private string _touchDisplayPort;
  private string _touchDeviceName;
  private const int EXPECTED_TOUCH_WIDTH = 1920;
  private const int EXPECTED_TOUCH_HEIGHT = 1080;

  private void Start()
  {
    if (!DetectDisplayConfiguration())
    {
      Debug.LogError("Failed to detect valid display configuration.");
      OnError?.Invoke("Failed to detect valid display configuration.");
      return;
    }

    if (!DetectTouchDevice())
    {
      Debug.LogError("Failed to detect touch input device.");
      OnError?.Invoke("Failed to detect touch input device.");
      return;
    }

    string xinputListOutput = ExecuteCommand("xinput list");
    int deviceId = GetDeviceId(xinputListOutput);
    if (deviceId != -1)
    {
      Debug.Log($"Mapping touch device '{_touchDeviceName}' (ID: {deviceId}) to display {_touchDisplayPort}");

      var (displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight) = GetDisplayDimensions(_touchDisplayPort);
      MapDeviceToOutput(deviceId, _touchDisplayPort);
      SetCoordinateTransformationMatrix(deviceId, displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight);
    }
  }

  private class DisplayInfo
  {
    public string Port { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsLandscape => Width > Height;
    public override string ToString() => $"{Port}: {Width}x{Height} at +{X}+{Y}";
  }

  private bool DetectDisplayConfiguration()
  {
    string xrandrOutput = ExecuteCommand("xrandr");
    var displays = new List<DisplayInfo>();

    // Pattern to match connected displays, handling both normal and rotated configurations
    string connectedPattern = @"(\S+) connected(?: primary)? (?:(\d+)x(\d+)\+(\d+)\+(\d+)|(\d+)x(\d+)\+(\d+)\+(\d+) left)";
    var matches = Regex.Matches(xrandrOutput, connectedPattern);

    foreach (Match match in matches)
    {
      string port = match.Groups[1].Value;
      int width, height, x, y;

      if (match.Groups[2].Success)  // Normal orientation
      {
        width = int.Parse(match.Groups[2].Value);
        height = int.Parse(match.Groups[3].Value);
        x = int.Parse(match.Groups[4].Value);
        y = int.Parse(match.Groups[5].Value);
      }
      else  // Rotated (left) orientation - swap width and height
      {
        // For left rotation, the reported resolution is already swapped
        width = int.Parse(match.Groups[6].Value);
        height = int.Parse(match.Groups[7].Value);
        x = int.Parse(match.Groups[8].Value);
        y = int.Parse(match.Groups[9].Value);
      }

      displays.Add(new DisplayInfo
      {
        Port = port,
        Width = width,
        Height = height,
        X = x,
        Y = y
      });
    }

    // We need at least two displays
    if (displays.Count < 2)
    {
      Debug.LogError($"Not enough displays detected. Found {displays.Count} displays: {string.Join(", ", displays)}");
      return false;
    }

    // Find the landscape display which is likely to be the touch screen
    var landscapeDisplay = displays.FirstOrDefault(d => d.IsLandscape);
    if (landscapeDisplay == null)
    {
      Debug.LogError("No landscape display detected.");
      return false;
    }

    _touchDisplayPort = landscapeDisplay.Port;
    Debug.Log($"Detected touch display on port: {_touchDisplayPort} ({landscapeDisplay.Width}x{landscapeDisplay.Height})");
    return true;
  }

  private bool DetectTouchDevice()
  {
    string xinputListOutput = ExecuteCommand("xinput list");

    // Pattern to match input devices with "touch" in their name (case insensitive)
    string touchPattern = @"↳\s+(.*?(?:touch|mtouch).*?)\s+id=\d+\s+\[slave\s+pointer";
    var match = Regex.Match(xinputListOutput, touchPattern, RegexOptions.IgnoreCase);

    if (match.Success)
    {
      _touchDeviceName = match.Groups[1].Value.Trim();
      Debug.Log($"Detected touch device: {_touchDeviceName}");
      return true;
    }

    return false;
  }

  private string ExecuteCommand(string command)
  {
    Debug.Log($"Executing command: bash -c {command}");
    var result = ExecuteCommandPlugin.RunCommand($"bash -c \"{command}\"");
    Debug.Log($"Command output: {result}");
    return result;
  }

  private int GetDeviceId(string xinputListOutput)
  {
    // Escape any special regex characters in the device name
    string escapedDeviceName = Regex.Escape(_touchDeviceName);
    string pattern = $@"{escapedDeviceName}\s+id=(\d+)";
    Match match = Regex.Match(xinputListOutput, pattern);

    if (match.Success && int.TryParse(match.Groups[1].Value, out int deviceId))
    {
      return deviceId;
    }

    Debug.LogError($"Touch device '{_touchDeviceName}' not found.");
    OnError?.Invoke($"Touch device '{_touchDeviceName}' not found.");
    return -1;
  }

  private void MapDeviceToOutput(int deviceId, string output)
  {
    if (deviceId < 0)
    {
      return;
    }

    string command = $"xinput map-to-output {deviceId} {output}";
    _ = ExecuteCommand(command);
  }

  private (float displayWidth, float displayHeight, float displayX, float displayY, float totalWidth, float totalHeight) GetDisplayDimensions(string outputDisplay)
  {
    string xrandrOutput = ExecuteCommand("xrandr");
    Debug.Log($"Xrandr: {xrandrOutput}");

    float displayWidth = EXPECTED_TOUCH_WIDTH;
    float displayHeight = EXPECTED_TOUCH_HEIGHT;
    float displayX = 0f;
    float displayY = 0f;
    float totalWidth = Screen.width;
    float totalHeight = Screen.height;

    // Get the specific display dimensions and position
    string displayPattern = $@"{Regex.Escape(outputDisplay)} connected.*?(\d+)x(\d+)\+(\d+)\+(\d+)";
    Match displayMatch = Regex.Match(xrandrOutput, displayPattern);

    if (displayMatch.Success)
    {
      displayWidth = float.Parse(displayMatch.Groups[1].Value);
      displayHeight = float.Parse(displayMatch.Groups[2].Value);
      displayX = float.Parse(displayMatch.Groups[3].Value);
      displayY = float.Parse(displayMatch.Groups[4].Value);
    }

    // Get the total screen dimensions
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

  private void SetCoordinateTransformationMatrix(int deviceId, float displayWidth, float displayHeight, float displayX, float displayY, float totalWidth, float totalHeight)
  {
    if (deviceId < 0)
    {
      return;
    }

    // Calculate scaling factors
    float scaleX = displayWidth / totalWidth;
    float scaleY = displayHeight / totalHeight;

    // Calculate offsets as proportion of total dimensions
    float offsetX = displayX / totalWidth;
    float offsetY = displayY / totalHeight;

    string matrix = $"{scaleX} 0 {offsetX} 0 {scaleY} {offsetY} 0 0 1";
    Debug.Log($"Setting transformation matrix: {matrix}");

    string command = $"xinput set-prop {deviceId} 'Coordinate Transformation Matrix' {matrix}";
    _ = ExecuteCommand(command);
  }
#endif
}