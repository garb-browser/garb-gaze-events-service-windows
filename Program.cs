using System;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using Interaction_Interactors_101.Providers;
using Interaction_Interactors_101.Services;
using Newtonsoft.Json;
using Tobii.Interaction;
using Tobii.Interaction.Framework;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Interaction_Interactors_101
{
    /// <summary>
    /// GARB Eye Tracker WebSocket Server
    /// Supports both Tobii Consumer (Eye Tracker 5) and Tobii Pro (Pro Nano) devices.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// WebSocket endpoint for detecting available eye tracker devices.
        /// Returns JSON array of device info, then closes the connection.
        /// </summary>
        public class DevicesEndpoint : WebSocketBehavior
        {
            protected override void OnOpen()
            {
                Console.WriteLine("[Devices] Client requesting device list at " + DateTime.Now);

                try
                {
                    var service = new DeviceDetectionService();
                    var devices = service.DetectAllDevices();

                    // Convert to JSON and send
                    var json = JsonConvert.SerializeObject(devices);
                    Send(json);

                    Console.WriteLine($"[Devices] Sent {devices.Count} device(s) to client");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Devices] Error detecting devices: " + ex.Message);
                    Send("[]"); // Return empty array on error
                }

                // Close connection after sending device list
                Context.WebSocket.Close();
            }
        }

        /// <summary>
        /// Main gaze streaming WebSocket endpoint.
        /// Accepts ?device=consumer|pro query parameter to select eye tracker.
        /// Defaults to Consumer (Eye Tracker 5) for backward compatibility.
        /// </summary>
        public class GazeEndpoint : WebSocketBehavior
        {
            private IGazeProvider _provider;
            private int _gazeMessagesSent = 0;
            private DateTime _lastGazeLog = DateTime.MinValue;
            private bool _firstGazeSent = false;

            protected override void OnMessage(MessageEventArgs e)
            {
                // Handle messages from the extension if needed
                Console.WriteLine("[Gaze] Received from extension: " + e.Data);
            }

            private void SafeSend(string message)
            {
                try
                {
                    if (State == WebSocketState.Open)
                    {
                        Send(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Gaze] Error sending message: " + ex.Message);
                }
            }

            protected override void OnOpen()
            {
                Console.WriteLine("[Gaze] WebSocket client connected at " + DateTime.Now);

                try
                {
                    // Parse query string to determine device type
                    string deviceType = GetDeviceTypeFromQuery();

                    Console.WriteLine($"[Gaze] Requested device type: {deviceType}");

                    // Create the appropriate provider using factory pattern
                    _provider = CreateProvider(deviceType);

                    if (_provider == null)
                    {
                        SafeSend("error|No suitable eye tracker provider available");
                        return;
                    }

                    // Subscribe to gaze data events
                    _provider.GazeDataReceived += OnGazeData;
                    _provider.ConnectionStatusChanged += OnConnectionStatusChanged;

                    // Connect to the device
                    _provider.Connect();

                    if (!_provider.IsConnected)
                    {
                        SafeSend($"error|Failed to connect to {_provider.DeviceName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Gaze] ERROR: Failed to initialize eye tracker!");
                    Console.WriteLine("[Gaze] Error details: " + ex.Message);
                    SafeSend("error|" + ex.Message);
                }
            }

            /// <summary>
            /// Parse device type from query string. Defaults to "consumer" for backward compatibility.
            /// </summary>
            private string GetDeviceTypeFromQuery()
            {
                try
                {
                    var queryString = Context.QueryString;
                    if (queryString != null && queryString["device"] != null)
                    {
                        return queryString["device"].ToLower();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Gaze] Error parsing query string: " + ex.Message);
                }

                // Default to consumer for backward compatibility
                return "consumer";
            }

            /// <summary>
            /// Factory method to create the appropriate gaze provider
            /// </summary>
            private IGazeProvider CreateProvider(string deviceType)
            {
                switch (deviceType.ToLower())
                {
                    case "pro":
                        Console.WriteLine("[Gaze] Creating Tobii Pro provider...");
                        return new TobiiProProvider();

                    case "consumer":
                    default:
                        Console.WriteLine("[Gaze] Creating Tobii Consumer provider...");
                        return new TobiiConsumerProvider();
                }
            }

            /// <summary>
            /// Handle gaze data from the provider and send to WebSocket client
            /// </summary>
            private void OnGazeData(object sender, GarbGazeEventArgs e)
            {
                string message;

                if (e.HasProData)
                {
                    // Extended Pro format with all available data
                    // Format: gaze|x|y|pupilL|pupilR|originLX|originLY|originLZ|originRX|originRY|originRZ|validL|validR
                    message = string.Format("gaze|{0:F1}|{1:F1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}",
                        e.X,
                        e.Y,
                        FormatNullable(e.PupilLeftDiameter, "F2"),
                        FormatNullable(e.PupilRightDiameter, "F2"),
                        FormatNullable(e.GazeOriginLeftX, "F3"),
                        FormatNullable(e.GazeOriginLeftY, "F3"),
                        FormatNullable(e.GazeOriginLeftZ, "F3"),
                        FormatNullable(e.GazeOriginRightX, "F3"),
                        FormatNullable(e.GazeOriginRightY, "F3"),
                        FormatNullable(e.GazeOriginRightZ, "F3"),
                        FormatNullable(e.LeftEyeValidity, "F0"),
                        FormatNullable(e.RightEyeValidity, "F0"));
                }
                else
                {
                    // Standard Consumer format (backward compatible)
                    // Format: gaze|x|y
                    message = string.Format("gaze|{0}|{1}", e.X, e.Y);
                }

                SafeSend(message);
                _gazeMessagesSent++;

                // Log first message to confirm data flow
                if (!_firstGazeSent)
                {
                    Console.WriteLine($"[Gaze] First message sent to extension: {message.Substring(0, Math.Min(80, message.Length))}...");
                    _firstGazeSent = true;
                }

                // Log periodically to avoid spam
                var now = DateTime.Now;
                if ((now - _lastGazeLog).TotalSeconds >= 5)
                {
                    Console.WriteLine($"[Gaze] Messages sent: {_gazeMessagesSent}, HasProData: {e.HasProData}");
                    _lastGazeLog = now;
                }
            }

            /// <summary>
            /// Format a nullable double for transmission
            /// </summary>
            private string FormatNullable(double? value, string format)
            {
                return value.HasValue ? value.Value.ToString(format) : "";
            }

            /// <summary>
            /// Handle connection status changes
            /// </summary>
            private void OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
            {
                if (!e.IsConnected && !string.IsNullOrEmpty(e.Message))
                {
                    SafeSend("error|" + e.Message);
                }
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Console.WriteLine("[Gaze] WebSocket client disconnected at " + DateTime.Now);

                try
                {
                    if (_provider != null)
                    {
                        _provider.GazeDataReceived -= OnGazeData;
                        _provider.ConnectionStatusChanged -= OnConnectionStatusChanged;
                        _provider.Dispose();
                        _provider = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Gaze] Error during cleanup: " + ex.Message);
                }

                base.OnClose(e);
            }

            protected override void OnError(ErrorEventArgs e)
            {
                Console.WriteLine("[Gaze] WebSocket error: " + e.Message);
                base.OnError(e);
            }
        }

        // ============================================================================
        // LEGACY ENDPOINT - Keep for backward compatibility
        // This is the original implementation that connects directly without provider
        // ============================================================================
        public class Laputa : WebSocketBehavior
        {
            Tobii.Interaction.FixationDataStream fixationDataStream;
            Tobii.Interaction.GazePointDataStream gazePointDataStream;
            Tobii.Interaction.Host host;
            double fixationBeginTime;
            bool receivedEndFixation = true;

            private static readonly bool USE_GAZE_POINT_STREAM = true;

            protected override void OnMessage(MessageEventArgs e)
            {
                Console.WriteLine("Received from extension: " + e.Data);
            }

            private void SafeSend(string message)
            {
                try
                {
                    if (State == WebSocketState.Open)
                    {
                        Send(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending message: " + ex.Message);
                }
            }

            private void handleGazePoint(object sender, Tobii.Interaction.StreamData<Tobii.Interaction.GazePointData> gazePoint)
            {
                var x = gazePoint.Data.X;
                var y = gazePoint.Data.Y;

                if (double.IsNaN(x) || double.IsNaN(y))
                    return;

                string gazeString = string.Format("gaze|{0}|{1}", x, y);
                SafeSend(gazeString);
            }

            private void handleFixation(object sender, Tobii.Interaction.StreamData<Tobii.Interaction.FixationData> fixation)
            {
                var fixationPointX = fixation.Data.X;
                var fixationPointY = fixation.Data.Y;

                if (double.IsNaN(fixationPointX) || double.IsNaN(fixationPointY))
                    return;

                switch (fixation.Data.EventType)
                {
                    case FixationDataEventType.Begin:
                        if (!receivedEndFixation)
                        {
                            string durationString = string.Format("duration|{0}|null",
                            fixationBeginTime > 0
                                ? TimeSpan.FromMilliseconds(fixation.Data.Timestamp - fixationBeginTime)
                                : TimeSpan.Zero);
                            receivedEndFixation = true;
                            SafeSend(durationString);
                        }

                        fixationBeginTime = fixation.Data.Timestamp;
                        string beginString = string.Format("begin|{0}|{1}", fixationPointX, fixationPointY);
                        receivedEndFixation = false;
                        SafeSend(beginString);
                        break;

                    case FixationDataEventType.Data:
                        string duringString = string.Format("during|{0}|{1}", fixationPointX, fixationPointY);
                        SafeSend(duringString);
                        break;

                    case FixationDataEventType.End:
                        string endString = string.Format("end|{0}|{1}", fixationPointX, fixationPointY);
                        SafeSend(endString);
                        string durationStringEnd = string.Format("duration|{0}|null",
                            fixationBeginTime > 0
                                ? TimeSpan.FromMilliseconds(fixation.Data.Timestamp - fixationBeginTime)
                                : TimeSpan.Zero);
                        receivedEndFixation = true;
                        SafeSend(durationStringEnd);
                        break;

                    default:
                        Console.WriteLine("Unknown fixation event type: " + fixation.Data.EventType);
                        break;
                }
            }

            protected override void OnOpen()
            {
                Console.WriteLine("WebSocket client connected at " + DateTime.Now);

                try
                {
                    host = new Tobii.Interaction.Host();

                    if (USE_GAZE_POINT_STREAM)
                    {
                        gazePointDataStream = host.Streams.CreateGazePointDataStream();
                        gazePointDataStream.Next += handleGazePoint;
                        Console.WriteLine("Gaze point stream opened at " + DateTime.Now + " (smooth mode)");
                    }
                    else
                    {
                        fixationDataStream = host.Streams.CreateFixationDataStream();
                        fixationBeginTime = 0d;
                        fixationDataStream.Next += handleFixation;
                        Console.WriteLine("Fixation stream opened at " + DateTime.Now + " (fixation mode)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: Failed to connect to Tobii eye tracker!");
                    Console.WriteLine("Make sure:");
                    Console.WriteLine("  1. Tobii Eye Tracker is connected via USB");
                    Console.WriteLine("  2. Tobii Experience (or Tobii Core) software is running");
                    Console.WriteLine("  3. Eye tracker is calibrated");
                    Console.WriteLine("Error details: " + ex.Message);

                    SafeSend("error|Tobii eye tracker not available");
                }
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Console.WriteLine("WebSocket client disconnected at " + DateTime.Now);

                try
                {
                    if (gazePointDataStream != null)
                    {
                        gazePointDataStream.Next -= handleGazePoint;
                        gazePointDataStream = null;
                    }
                    if (fixationDataStream != null)
                    {
                        fixationDataStream.Next -= handleFixation;
                        fixationDataStream = null;
                    }
                    if (host != null)
                    {
                        host.DisableConnection();
                        host = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error during cleanup: " + ex.Message);
                }

                base.OnClose(e);
            }

            protected override void OnError(ErrorEventArgs e)
            {
                Console.WriteLine("WebSocket error: " + e.Message);
                base.OnError(e);
            }
        }

        public static void Main(string[] args)
        {
            PrintBanner();

            WebSocketServer wssv = null;
            try
            {
                wssv = new WebSocketServer("ws://localhost:8765");

                // Device detection endpoint
                wssv.AddWebSocketService<DevicesEndpoint>("/devices");

                // Main gaze streaming endpoint (legacy, for backward compatibility)
                // Connects to /hello without query params = Consumer mode
                wssv.AddWebSocketService<Laputa>("/hello");

                // New gaze endpoint with device selection support
                // Connects to /gaze?device=consumer|pro
                wssv.AddWebSocketService<GazeEndpoint>("/gaze");

                wssv.Start();

                Console.WriteLine("Press any key to stop the server...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR starting server: " + ex.Message);
                Console.WriteLine("Port 8765 may already be in use.");
            }
            finally
            {
                if (wssv != null && wssv.IsListening)
                {
                    wssv.Stop();
                    Console.WriteLine("Server stopped.");
                }
            }
        }

        private static void PrintBanner()
        {
            Console.Clear();
            Console.WriteLine("============================================================");
            Console.WriteLine("|     GARB Eye Tracker WebSocket Server                    |");
            Console.WriteLine("|     Supports: Tobii Eye Tracker 5 + Tobii Pro Nano       |");
            Console.WriteLine("============================================================");
            Console.WriteLine();
            Console.WriteLine("Endpoints:");
            Console.WriteLine("  ws://localhost:8765/devices  - Get available devices (JSON)");
            Console.WriteLine("  ws://localhost:8765/hello    - Gaze stream (Consumer, legacy)");
            Console.WriteLine("  ws://localhost:8765/gaze     - Gaze stream with device selection");
            Console.WriteLine("                                 ?device=consumer (Eye Tracker 5)");
            Console.WriteLine("                                 ?device=pro (Pro Nano)");
            Console.WriteLine();
            Console.WriteLine("Server starting...");
        }

    }
}
