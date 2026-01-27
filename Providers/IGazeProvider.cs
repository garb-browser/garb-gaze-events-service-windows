using System;

namespace Interaction_Interactors_101.Providers
{
    /// <summary>
    /// Interface for eye tracker gaze data providers.
    /// Abstracts the differences between Consumer (Eye Tracker 5) and Pro (Pro Nano) SDKs.
    /// </summary>
    public interface IGazeProvider : IDisposable
    {
        /// <summary>
        /// Unique identifier for the device (e.g., "tobii-consumer" or device serial)
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// Human-readable device name (e.g., "Tobii Eye Tracker 5", "Tobii Pro Nano")
        /// </summary>
        string DeviceName { get; }

        /// <summary>
        /// Device type: "Consumer" or "Pro"
        /// </summary>
        string DeviceType { get; }

        /// <summary>
        /// Whether the device is currently connected and streaming
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Fired when new gaze data is available
        /// </summary>
        event EventHandler<GarbGazeEventArgs> GazeDataReceived;

        /// <summary>
        /// Fired when connection status changes
        /// </summary>
        event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Connect to the eye tracker and start streaming gaze data
        /// </summary>
        void Connect();

        /// <summary>
        /// Stop streaming and disconnect from the eye tracker
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Get detailed information about the connected device
        /// </summary>
        DeviceInfo GetDeviceInfo();
    }

    /// <summary>
    /// Gaze data event arguments with support for extended Pro data
    /// </summary>
    public class GarbGazeEventArgs : EventArgs
    {
        /// <summary>Screen X coordinate in pixels</summary>
        public double X { get; set; }

        /// <summary>Screen Y coordinate in pixels</summary>
        public double Y { get; set; }

        /// <summary>Timestamp in milliseconds</summary>
        public double Timestamp { get; set; }

        // Extended Pro fields (null for Consumer SDK)

        /// <summary>Left eye pupil diameter in mm (Pro only)</summary>
        public double? PupilLeftDiameter { get; set; }

        /// <summary>Right eye pupil diameter in mm (Pro only)</summary>
        public double? PupilRightDiameter { get; set; }

        /// <summary>Left eye gaze origin X in mm from eye tracker (Pro only)</summary>
        public double? GazeOriginLeftX { get; set; }

        /// <summary>Left eye gaze origin Y in mm from eye tracker (Pro only)</summary>
        public double? GazeOriginLeftY { get; set; }

        /// <summary>Left eye gaze origin Z in mm from eye tracker (Pro only)</summary>
        public double? GazeOriginLeftZ { get; set; }

        /// <summary>Right eye gaze origin X in mm from eye tracker (Pro only)</summary>
        public double? GazeOriginRightX { get; set; }

        /// <summary>Right eye gaze origin Y in mm from eye tracker (Pro only)</summary>
        public double? GazeOriginRightY { get; set; }

        /// <summary>Right eye gaze origin Z in mm from eye tracker (Pro only)</summary>
        public double? GazeOriginRightZ { get; set; }

        /// <summary>Left eye validity (1.0 = valid, 0.0 = invalid) (Pro only)</summary>
        public double? LeftEyeValidity { get; set; }

        /// <summary>Right eye validity (1.0 = valid, 0.0 = invalid) (Pro only)</summary>
        public double? RightEyeValidity { get; set; }

        /// <summary>
        /// Returns true if this event contains extended Pro data
        /// </summary>
        public bool HasProData => PupilLeftDiameter.HasValue || PupilRightDiameter.HasValue;
    }

    /// <summary>
    /// Connection status change event arguments
    /// </summary>
    public class ConnectionStatusEventArgs : EventArgs
    {
        /// <summary>Whether the device is now connected</summary>
        public bool IsConnected { get; set; }

        /// <summary>Status message (e.g., error details)</summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Information about an eye tracker device
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>Unique device identifier</summary>
        public string DeviceId { get; set; }

        /// <summary>Human-readable device name</summary>
        public string DeviceName { get; set; }

        /// <summary>Device type: "Consumer" or "Pro"</summary>
        public string DeviceType { get; set; }

        /// <summary>Device serial number (Pro only)</summary>
        public string SerialNumber { get; set; }

        /// <summary>Firmware version (Pro only)</summary>
        public string FirmwareVersion { get; set; }

        /// <summary>Nominal sampling rate in Hz</summary>
        public double SamplingRate { get; set; }

        /// <summary>Whether the device supports pupil diameter data</summary>
        public bool SupportsPupilData { get; set; }

        /// <summary>Whether the device supports 3D gaze origin data</summary>
        public bool SupportsGazeOrigin { get; set; }
    }
}
