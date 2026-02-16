using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Tobii Pro SDK uses Tobii.Research namespace
// Install via NuGet: Tobii.Research.x64
#if TOBII_PRO_SDK
using Tobii.Research;
#endif

namespace Interaction_Interactors_101.Providers
{
    /// <summary>
    /// Gaze provider for Tobii Pro devices (Pro Nano, Pro Fusion, Pro Spectrum, etc.)
    /// Uses the Tobii.Research SDK (Tobii Pro SDK).
    /// Provides extended data: pupil diameter, gaze origin (3D eye position), validity flags.
    /// </summary>
    public class TobiiProProvider : IGazeProvider
    {
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;

        // Y-axis offset to compensate for calibration differences (negative = move UP, positive = move DOWN)
        // This is in LOGICAL pixels (after DPI scaling)
        // Set to 0 since dynamic screen detection should handle positioning correctly
        private const double Y_OFFSET_PIXELS = 0.0;

#if TOBII_PRO_SDK
        private IEyeTracker _eyeTracker;
#endif
        private bool _disposed;
        private string _deviceUri;

        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }
        public string DeviceType => "Pro";
        public bool IsConnected { get; private set; }

        public event EventHandler<GarbGazeEventArgs> GazeDataReceived;
        public event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Create a Pro provider. Optionally specify a device URI to connect to a specific device.
        /// If no URI is provided, connects to the first available Pro device.
        /// </summary>
        /// <param name="deviceUri">Optional device URI (e.g., "tet-tcp://172.28.195.1")</param>
        public TobiiProProvider(string deviceUri = null)
        {
            _deviceUri = deviceUri;
            DeviceId = "tobii-pro";
            DeviceName = "Tobii Pro Device";
        }

        /// <summary>
        /// Connect to the Tobii Pro eye tracker and start streaming gaze data.
        /// Requires Tobii Pro Eye Tracker Manager for calibration.
        /// </summary>
        public void Connect()
        {
#if TOBII_PRO_SDK
            if (IsConnected)
            {
                return;
            }

            try
            {
                // Find all connected Pro eye trackers
                var eyeTrackers = EyeTrackingOperations.FindAllEyeTrackers();

                if (eyeTrackers.Count == 0)
                {
                    throw new Exception("No Tobii Pro eye tracker found. Make sure the device is connected via USB.");
                }

                // Select the device (use specified URI or first available)
                if (!string.IsNullOrEmpty(_deviceUri))
                {
                    _eyeTracker = eyeTrackers.FirstOrDefault(t => t.Address.ToString() == _deviceUri);
                    if (_eyeTracker == null)
                    {
                        throw new Exception($"Tobii Pro device with URI '{_deviceUri}' not found.");
                    }
                }
                else
                {
                    _eyeTracker = eyeTrackers[0];
                }

                // Update device info
                DeviceId = _eyeTracker.Address.ToString();
                DeviceName = $"Tobii Pro {_eyeTracker.Model}";

                // Subscribe to gaze data
                _eyeTracker.GazeDataReceived += OnGazeDataReceived;

                IsConnected = true;
                OnConnectionStatusChanged(true, $"Connected to {DeviceName}");

                Console.WriteLine($"[Pro] Connected to {DeviceName} (Serial: {_eyeTracker.SerialNumber}) at {DateTime.Now}");
                Console.WriteLine($"[Pro] Device address: {_eyeTracker.Address}");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                OnConnectionStatusChanged(false, ex.Message);

                Console.WriteLine("[Pro] ERROR: Failed to connect to Tobii Pro eye tracker!");
                Console.WriteLine("[Pro] Make sure:");
                Console.WriteLine("  1. Tobii Pro device is connected via USB");
                Console.WriteLine("  2. Tobii Pro Eye Tracker Manager is installed (for calibration)");
                Console.WriteLine("  3. The eye tracker is calibrated");
                Console.WriteLine("[Pro] Error details: " + ex.Message);
            }
#else
            // Pro SDK not available
            IsConnected = false;
            OnConnectionStatusChanged(false, "Tobii Pro SDK not installed. Install NuGet package Tobii.Research.x64 and rebuild with TOBII_PRO_SDK defined.");
            Console.WriteLine("[Pro] ERROR: Tobii Pro SDK not available. Rebuild with TOBII_PRO_SDK compiler directive.");
#endif
        }

        /// <summary>
        /// Stop streaming and disconnect from the eye tracker
        /// </summary>
        public void Disconnect()
        {
#if TOBII_PRO_SDK
            if (!IsConnected)
            {
                return;
            }

            try
            {
                if (_eyeTracker != null)
                {
                    _eyeTracker.GazeDataReceived -= OnGazeDataReceived;
                    _eyeTracker = null;
                }

                IsConnected = false;
                OnConnectionStatusChanged(false, "Disconnected");

                Console.WriteLine("[Pro] Disconnected at " + DateTime.Now);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Pro] Error during disconnect: " + ex.Message);
            }
#endif
        }

        /// <summary>
        /// Get information about this device
        /// </summary>
        public DeviceInfo GetDeviceInfo()
        {
#if TOBII_PRO_SDK
            return new DeviceInfo
            {
                DeviceId = DeviceId,
                DeviceName = DeviceName,
                DeviceType = DeviceType,
                SerialNumber = _eyeTracker?.SerialNumber,
                FirmwareVersion = _eyeTracker?.FirmwareVersion,
                SamplingRate = 60, // Pro Nano is 60Hz
                SupportsPupilData = true,
                SupportsGazeOrigin = true
            };
#else
            return new DeviceInfo
            {
                DeviceId = DeviceId,
                DeviceName = DeviceName,
                DeviceType = DeviceType,
                SamplingRate = 60,
                SupportsPupilData = true,
                SupportsGazeOrigin = true
            };
#endif
        }

#if TOBII_PRO_SDK
        private int _gazeEventCount = 0;
        private int _invalidEyeCount = 0;
        private int _nanCoordCount = 0;
        private DateTime _lastDebugLog = DateTime.MinValue;
        private bool _firstEventLogged = false;
        private int _coordDebugCount = 0;

        /// <summary>
        /// Handle incoming gaze data from the Pro SDK
        /// </summary>
        private void OnGazeDataReceived(object sender, Tobii.Research.GazeDataEventArgs e)
        {
            try
            {
                _gazeEventCount++;

                // Log first event to confirm subscription is working
                if (!_firstEventLogged)
                {
                    Console.WriteLine("[Pro] First gaze event received! Subscription is working.");

                    // Log ALL screens to help diagnose multi-monitor issues
                    Console.WriteLine($"[Pro] Number of screens: {Screen.AllScreens.Length}");
                    for (int i = 0; i < Screen.AllScreens.Length; i++)
                    {
                        var scr = Screen.AllScreens[i];
                        string primary = scr.Primary ? " (PRIMARY)" : "";
                        Console.WriteLine($"[Pro] Screen {i}{primary}: {scr.Bounds.Width}x{scr.Bounds.Height} at ({scr.Bounds.X},{scr.Bounds.Y})");
                    }

                    var selectedScreen = GetTobiiCalibratedScreen();
                    Console.WriteLine($"[Pro] Using screen for Tobii: {selectedScreen.Bounds.Width}x{selectedScreen.Bounds.Height} at ({selectedScreen.Bounds.X},{selectedScreen.Bounds.Y})");
                    Console.WriteLine($"[Pro] DPI scale: {GetDpiScale():F2}");
                    _firstEventLogged = true;
                }

                // Get gaze point (normalized 0-1 coordinates)
                var leftGaze = e.LeftEye.GazePoint.PositionOnDisplayArea;
                var rightGaze = e.RightEye.GazePoint.PositionOnDisplayArea;

                // Average the two eyes for combined gaze point
                // Use whichever eye is valid, or average if both are valid
                double gazeX, gazeY;
                bool leftValid = e.LeftEye.GazePoint.Validity == Validity.Valid;
                bool rightValid = e.RightEye.GazePoint.Validity == Validity.Valid;

                if (leftValid && rightValid)
                {
                    gazeX = (leftGaze.X + rightGaze.X) / 2.0;
                    gazeY = (leftGaze.Y + rightGaze.Y) / 2.0;
                }
                else if (leftValid)
                {
                    gazeX = leftGaze.X;
                    gazeY = leftGaze.Y;
                }
                else if (rightValid)
                {
                    gazeX = rightGaze.X;
                    gazeY = rightGaze.Y;
                }
                else
                {
                    // Both eyes invalid - skip this sample
                    _invalidEyeCount++;
                    LogDebugPeriodically();
                    return;
                }

                // Skip if coordinates are NaN
                if (double.IsNaN(gazeX) || double.IsNaN(gazeY))
                {
                    _nanCoordCount++;
                    LogDebugPeriodically();
                    return;
                }

                // Convert normalized coordinates (0-1) to screen pixels
                // Use the screen where Tobii is calibrated (not necessarily primary)
                var screenBounds = GetTobiiCalibratedScreen().Bounds;

                // Convert normalized coordinates to absolute desktop coordinates
                // then divide by DPI scale to get CSS/logical pixels
                double dpiScale = GetDpiScale();
                double screenX = (gazeX * screenBounds.Width + screenBounds.X) / dpiScale;
                double screenY = (gazeY * screenBounds.Height + screenBounds.Y) / dpiScale + Y_OFFSET_PIXELS;

                // Log first 5 coordinate conversions for debugging
                if (_coordDebugCount < 5)
                {
                    Console.WriteLine($"[Pro] Coord #{_coordDebugCount}: normalized({gazeX:F4},{gazeY:F4}) -> screen({screenX:F1},{screenY:F1})");
                    _coordDebugCount++;
                }

                // Get pupil data
                var leftPupil = e.LeftEye.Pupil;
                var rightPupil = e.RightEye.Pupil;

                // Get gaze origin (3D eye position in user coordinates - millimeters from eye tracker)
                var leftOrigin = e.LeftEye.GazeOrigin.PositionInUserCoordinates;
                var rightOrigin = e.RightEye.GazeOrigin.PositionInUserCoordinates;

                // Fire event with extended Pro data
                GazeDataReceived?.Invoke(this, new Providers.GarbGazeEventArgs
                {
                    X = screenX,
                    Y = screenY,
                    Timestamp = e.DeviceTimeStamp / 1000.0, // Convert microseconds to milliseconds

                    // Extended Pro data
                    PupilLeftDiameter = leftPupil.Validity == Validity.Valid ? leftPupil.PupilDiameter : (double?)null,
                    PupilRightDiameter = rightPupil.Validity == Validity.Valid ? rightPupil.PupilDiameter : (double?)null,

                    // Gaze origin (3D position) - left eye
                    GazeOriginLeftX = e.LeftEye.GazeOrigin.Validity == Validity.Valid ? leftOrigin.X : (double?)null,
                    GazeOriginLeftY = e.LeftEye.GazeOrigin.Validity == Validity.Valid ? leftOrigin.Y : (double?)null,
                    GazeOriginLeftZ = e.LeftEye.GazeOrigin.Validity == Validity.Valid ? leftOrigin.Z : (double?)null,

                    // Gaze origin (3D position) - right eye
                    GazeOriginRightX = e.RightEye.GazeOrigin.Validity == Validity.Valid ? rightOrigin.X : (double?)null,
                    GazeOriginRightY = e.RightEye.GazeOrigin.Validity == Validity.Valid ? rightOrigin.Y : (double?)null,
                    GazeOriginRightZ = e.RightEye.GazeOrigin.Validity == Validity.Valid ? rightOrigin.Z : (double?)null,

                    // Validity flags
                    LeftEyeValidity = leftValid ? 1.0 : 0.0,
                    RightEyeValidity = rightValid ? 1.0 : 0.0
                });

                // Log successful send periodically
                LogDebugPeriodically();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Pro] Error processing gaze data: " + ex.Message);
            }
        }

        /// <summary>
        /// Log debug stats periodically (every 5 seconds) to avoid console spam
        /// </summary>
        private void LogDebugPeriodically()
        {
            var now = DateTime.Now;
            if ((now - _lastDebugLog).TotalSeconds >= 5)
            {
                int validCount = _gazeEventCount - _invalidEyeCount - _nanCoordCount;
                Console.WriteLine($"[Pro] Gaze stats: {_gazeEventCount} events, {validCount} valid, {_invalidEyeCount} invalid eyes, {_nanCoordCount} NaN coords");
                _lastDebugLog = now;
            }
        }

        /// <summary>
        /// Get the system DPI scaling factor.
        /// Returns 1.0 for 100%, 1.25 for 125%, 1.5 for 150%, etc.
        /// </summary>
        private double GetDpiScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hdc = g.GetHdc();
                    int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
                    g.ReleaseHdc(hdc);
                    return dpi / 96.0;
                }
            }
            catch
            {
                return 1.0;
            }
        }

        private Screen _cachedTobiiScreen = null;

        /// <summary>
        /// Find the screen where the Tobii Pro is calibrated by querying the device's display area
        /// and matching its aspect ratio to available screens.
        /// </summary>
        private Screen GetTobiiCalibratedScreen()
        {
            if (_cachedTobiiScreen != null)
                return _cachedTobiiScreen;

            var screens = Screen.AllScreens;

            // If only one screen, use it
            if (screens.Length == 1)
            {
                _cachedTobiiScreen = screens[0];
                Console.WriteLine($"[Pro] Single screen setup: {_cachedTobiiScreen.Bounds.Width}x{_cachedTobiiScreen.Bounds.Height}");
                return _cachedTobiiScreen;
            }

            // Try to get the display area from the Tobii to determine which screen it's calibrated to
            if (_eyeTracker != null)
            {
                try
                {
                    var displayArea = _eyeTracker.GetDisplayArea();

                    // Calculate display dimensions from the 3D corner points (in mm)
                    // TopLeft, TopRight, BottomLeft are Point3D with X, Y, Z
                    double displayWidth = Math.Sqrt(
                        Math.Pow(displayArea.TopRight.X - displayArea.TopLeft.X, 2) +
                        Math.Pow(displayArea.TopRight.Y - displayArea.TopLeft.Y, 2) +
                        Math.Pow(displayArea.TopRight.Z - displayArea.TopLeft.Z, 2));

                    double displayHeight = Math.Sqrt(
                        Math.Pow(displayArea.BottomLeft.X - displayArea.TopLeft.X, 2) +
                        Math.Pow(displayArea.BottomLeft.Y - displayArea.TopLeft.Y, 2) +
                        Math.Pow(displayArea.BottomLeft.Z - displayArea.TopLeft.Z, 2));

                    double tobiiAspectRatio = displayWidth / displayHeight;
                    Console.WriteLine($"[Pro] Tobii display area: {displayWidth:F1}mm x {displayHeight:F1}mm (aspect ratio: {tobiiAspectRatio:F3})");

                    // Find the screen with the closest matching aspect ratio
                    Screen bestMatch = null;
                    double bestDiff = double.MaxValue;

                    foreach (var screen in screens)
                    {
                        double screenAspectRatio = (double)screen.Bounds.Width / screen.Bounds.Height;
                        double diff = Math.Abs(screenAspectRatio - tobiiAspectRatio);

                        Console.WriteLine($"[Pro] Screen {screen.Bounds.Width}x{screen.Bounds.Height} aspect ratio: {screenAspectRatio:F3}, diff: {diff:F4}");

                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestMatch = screen;
                        }
                    }

                    if (bestMatch != null && bestDiff < 0.1) // Allow 10% tolerance
                    {
                        _cachedTobiiScreen = bestMatch;
                        Console.WriteLine($"[Pro] Matched Tobii to screen: {_cachedTobiiScreen.Bounds.Width}x{_cachedTobiiScreen.Bounds.Height} (aspect diff: {bestDiff:F4})");
                        return _cachedTobiiScreen;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Pro] Could not query display area: {ex.Message}");
                }
            }

            // Fallback: use the largest screen by area (likely the main working monitor)
            _cachedTobiiScreen = screens.OrderByDescending(s => s.Bounds.Width * s.Bounds.Height).First();
            Console.WriteLine($"[Pro] Fallback to largest screen: {_cachedTobiiScreen.Bounds.Width}x{_cachedTobiiScreen.Bounds.Height}");
            return _cachedTobiiScreen;
        }
#endif

        /// <summary>
        /// Fire connection status changed event
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected, string message)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
            {
                IsConnected = isConnected,
                Message = message
            });
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Disconnect();
            _disposed = true;
        }
    }
}
