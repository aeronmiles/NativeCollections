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
  private List<TouchDeviceInfo> _touchDevices;
  private const int EXPECTED_TOUCH_WIDTH = 1920;
  private const int EXPECTED_TOUCH_HEIGHT = 1080;

  private class TouchDeviceInfo
  {
    public string Name { get; set; }
    public int Id { get; set; }
    public bool IsMainTouchDevice { get; set; }
    public override string ToString() => $"{Name} (ID: {Id})";
  }

  private void Start()
  {
    if (!DetectDisplayConfiguration())
    {
      Debug.LogError("Failed to detect valid display configuration.");
      OnError?.Invoke("Failed to detect valid display configuration.");
      return;
    }

    if (!DetectTouchDevices())
    {
      Debug.LogError("Failed to detect touch input devices.");
      OnError?.Invoke("Failed to detect touch input devices.");
      return;
    }

    var (displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight) = GetDisplayDimensions(_touchDisplayPort);

    foreach (var device in _touchDevices)
    {
      Debug.Log($"Mapping touch device '{device.Name}' (ID: {device.Id}) to display {_touchDisplayPort}");
      MapDeviceToOutput(device.Id, _touchDisplayPort);
      SetCoordinateTransformationMatrix(device.Id, displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight);
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

  private bool DetectTouchDevices()
  {
    string xinputListOutput = ExecuteCommand("xinput list");
    _touchDevices = new List<TouchDeviceInfo>();

    // Pattern to match all touch devices (both pointer and keyboard)
    string touchPattern = @"↳\s+([^\n]*?(?:touch|Touch|TOUCH)[^\n]*?)\s+id=(\d+)\s+\[slave\s+(pointer|keyboard)";
    var matches = Regex.Matches(xinputListOutput, touchPattern, RegexOptions.IgnoreCase);

    // Process matches and identify pointer devices only
    var touchDevices = matches.Cast<Match>()
        .Select(m => new
        {
          Name = m.Groups[1].Value.Trim(),
          Id = int.Parse(m.Groups[2].Value),
          Type = m.Groups[3].Value.ToLower()
        })
        .Where(x => x.Type == "pointer")  // Only include pointer devices
        .OrderBy(x => x.Id)
        .ToList();

    // Process only pointer devices for touch mapping
    foreach (var device in touchDevices)
    {
      string deviceName = device.Name;
      int deviceId = device.Id;

      // For single pointer devices, it's the main device
      bool isMainDevice = touchDevices.Count == 1 || device == touchDevices.First();

      _touchDevices.Add(new TouchDeviceInfo
      {
        Name = deviceName,
        Id = deviceId,
        IsMainTouchDevice = isMainDevice
      });

      Debug.Log($"Detected touch device: {deviceName} (ID: {deviceId}, Main: {isMainDevice})");
    }

    if (_touchDevices.Count == 0)
    {
      Debug.LogError("No touch devices found in xinput list.");
      return false;
    }

    return true;
  }

  private string ExecuteCommand(string command)
  {
    Debug.Log($"Executing command: bash -c {command}");
    var result = ExecuteCommandPlugin.RunCommand($"bash -c \"{command}\"");
    Debug.Log($"Command output: {result}");
    return result;
  }

  private void MapDeviceToOutput(int deviceId, string output)
  {
    if (deviceId < 0)
    {
      Debug.LogError($"Invalid device ID: {deviceId}");
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
    else
    {
      Debug.LogWarning($"Could not find display dimensions for {outputDisplay}, using defaults");
    }

    // Get the total screen dimensions
    string screenPattern = @"current (\d+) x (\d+)";
    Match screenMatch = Regex.Match(xrandrOutput, screenPattern);

    if (screenMatch.Success)
    {
      totalWidth = float.Parse(screenMatch.Groups[1].Value);
      totalHeight = float.Parse(screenMatch.Groups[2].Value);
    }
    else
    {
      Debug.LogWarning("Could not find total screen dimensions, using Unity Screen values");
    }

    Debug.Log($"Display: {displayWidth}x{displayHeight}+{displayX}+{displayY}, Screen: {totalWidth}x{totalHeight}");
    return (displayWidth, displayHeight, displayX, displayY, totalWidth, totalHeight);
  }

  private void SetCoordinateTransformationMatrix(int deviceId, float displayWidth, float displayHeight, float displayX, float displayY, float totalWidth, float totalHeight)
  {
    if (deviceId < 0)
    {
      Debug.LogError($"Invalid device ID: {deviceId}");
      return;
    }

    // Calculate scaling factors
    float scaleX = displayWidth / totalWidth;
    float scaleY = displayHeight / totalHeight;

    // Calculate offsets as proportion of total dimensions
    float offsetX = displayX / totalWidth;
    float offsetY = displayY / totalHeight;

    string matrix = $"{scaleX} 0 {offsetX} 0 {scaleY} {offsetY} 0 0 1";
    Debug.Log($"Setting transformation matrix for device {deviceId}: {matrix}");

    string command = $"xinput set-prop {deviceId} 'Coordinate Transformation Matrix' {matrix}";
    _ = ExecuteCommand(command);
  }
#endif
}