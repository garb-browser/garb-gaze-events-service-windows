using System;
using Tobii.Interaction;
using Tobii.Interaction.Framework;

namespace Interaction_Interactors_101.Providers
{
    /// <summary>
    /// Gaze provider for Tobii Consumer devices (Eye Tracker 5, 4C, etc.)
    /// Uses the Tobii.Interaction SDK.
    /// </summary>
    public class TobiiConsumerProvider : IGazeProvider
    {
        private Host _host;
        private GazePointDataStream _gazeStream;
        private bool _disposed;

        public string DeviceId => "tobii-consumer";
        public string DeviceName => "Tobii Eye Tracker 5";
        public string DeviceType => "Consumer";
        public bool IsConnected { get; private set; }

        public event EventHandler<GarbGazeEventArgs> GazeDataReceived;
        public event EventHandler<ConnectionStatusEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Connect to the Tobii Consumer eye tracker and start streaming gaze data.
        /// Requires Tobii Experience (or Tobii Core) software to be running.
        /// </summary>
        public void Connect()
        {
            if (IsConnected)
            {
                return;
            }

            try
            {
                // Initialize connection to Tobii Engine
                _host = new Host();

                // Create gaze point data stream (smooth mode)
                _gazeStream = _host.Streams.CreateGazePointDataStream();
                _gazeStream.Next += OnGazePoint;

                IsConnected = true;
                OnConnectionStatusChanged(true, "Connected to Tobii Eye Tracker");

                Console.WriteLine("[Consumer] Connected to Tobii Eye Tracker at " + DateTime.Now);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                OnConnectionStatusChanged(false, ex.Message);

                Console.WriteLine("[Consumer] ERROR: Failed to connect to Tobii eye tracker!");
                Console.WriteLine("[Consumer] Make sure:");
                Console.WriteLine("  1. Tobii Eye Tracker is connected via USB");
                Console.WriteLine("  2. Tobii Experience (or Tobii Core) software is running");
                Console.WriteLine("  3. Eye tracker is calibrated");
                Console.WriteLine("[Consumer] Error details: " + ex.Message);
            }
        }

        /// <summary>
        /// Stop streaming and disconnect from the eye tracker
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                if (_gazeStream != null)
                {
                    _gazeStream.Next -= OnGazePoint;
                    _gazeStream = null;
                }

                if (_host != null)
                {
                    _host.DisableConnection();
                    _host = null;
                }

                IsConnected = false;
                OnConnectionStatusChanged(false, "Disconnected");

                Console.WriteLine("[Consumer] Disconnected at " + DateTime.Now);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Consumer] Error during disconnect: " + ex.Message);
            }
        }

        /// <summary>
        /// Get information about this device
        /// </summary>
        public DeviceInfo GetDeviceInfo()
        {
            return new DeviceInfo
            {
                DeviceId = DeviceId,
                DeviceName = DeviceName,
                DeviceType = DeviceType,
                SerialNumber = null, // Not available from Consumer SDK
                FirmwareVersion = null,
                SamplingRate = 60, // Typical for Eye Tracker 5
                SupportsPupilData = false,
                SupportsGazeOrigin = false
            };
        }

        /// <summary>
        /// Handle incoming gaze point data from the Consumer SDK.
        /// Sends raw screen coordinates - DPR conversion is handled in JavaScript.
        /// </summary>
        private void OnGazePoint(object sender, StreamData<GazePointData> data)
        {
            // Skip invalid data (NaN occurs when eyes are not detected)
            if (double.IsNaN(data.Data.X) || double.IsNaN(data.Data.Y))
            {
                return;
            }

            // Send raw coordinates - JavaScript divides by window.devicePixelRatio
            GazeDataReceived?.Invoke(this, new GarbGazeEventArgs
            {
                X = data.Data.X,
                Y = data.Data.Y,
                Timestamp = data.Data.Timestamp,
                PupilLeftDiameter = null,
                PupilRightDiameter = null,
                GazeOriginLeftX = null,
                GazeOriginLeftY = null,
                GazeOriginLeftZ = null,
                GazeOriginRightX = null,
                GazeOriginRightY = null,
                GazeOriginRightZ = null,
                LeftEyeValidity = null,
                RightEyeValidity = null
            });
        }

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
