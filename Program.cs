using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tobii.Interaction;

using Tobii.Interaction.Framework;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Interaction_Interactors_101
{
    /// <summary>
    /// GARB Eye Tracker WebSocket Server
    /// Connects to Tobii eye tracker and streams gaze data to browser extensions.
    /// </summary>
    public class Program
    {
        public class Laputa : WebSocketBehavior
        {
            FixationDataStream fixationDataStream;
            GazePointDataStream gazePointDataStream;
            Host host;
            double fixationBeginTime;
            bool receivedEndFixation = true;

            // Set to true for smoother gaze tracking (more updates, like Tobii's preview trail)
            // Set to false for fixation-based tracking (fewer updates, only when eyes rest)
            // Change this value to switch between gaze point and fixation modes
            private static readonly bool USE_GAZE_POINT_STREAM = true;

            protected override void OnMessage(MessageEventArgs e)
            {
                // Handle messages from the extension if needed
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

            // Handler for raw gaze point data (smoother, more frequent updates)
            private void handleGazePoint(object sender, StreamData<GazePointData> gazePoint)
            {
                var x = gazePoint.Data.X;
                var y = gazePoint.Data.Y;

                // Skip invalid data (NaN values occur when eyes are not detected)
                if (double.IsNaN(x) || double.IsNaN(y))
                    return;

                // Send gaze data - this is like Tobii's preview trail
                string gazeString = string.Format("gaze|{0}|{1}", x, y);
                SafeSend(gazeString);
            }

            // Handler for fixation data (filtered, only when eyes rest on something)
            private void handleFixation(object sender, StreamData<FixationData> fixation)
            {
                var fixationPointX = fixation.Data.X;
                var fixationPointY = fixation.Data.Y;

                // Skip invalid data
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
                    // Everything starts with initializing Host, which manages the connection to the
                    // Tobii Engine and provides all the Tobii Core SDK functionality.
                    // NOTE: Make sure Tobii Experience app is running
                    host = new Host();

                    if (USE_GAZE_POINT_STREAM)
                    {
                        // Use GazePointDataStream for smoother tracking (like Tobii's preview trail)
                        gazePointDataStream = host.Streams.CreateGazePointDataStream();
                        gazePointDataStream.Next += handleGazePoint;
                        Console.WriteLine("Gaze point stream opened at " + DateTime.Now + " (smooth mode)");
                    }
                    else
                    {
                        // Use FixationDataStream for filtered tracking (only when eyes fixate)
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

                    // Send error to client
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
            PrintSampleIntroText();

            WebSocketServer wssv = null;
            try
            {
                wssv = new WebSocketServer("ws://localhost:8765");
                wssv.AddWebSocketService<Laputa>("/hello");
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

            /*
            // Everything starts with initializing Host, which manages the connection to the 
            // Tobii Engine and provides all the Tobii Core SDK functionality.
            // NOTE: Make sure that Tobii.EyeX.exe is running
            var host = new Host();

            PrintSampleIntroText();

            // InteractorAgents are defined per window, so we need a handle to it.
            var currentWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
            // Let's also obtain its bounds using Windows API calls (hidden in a helper method below).
            var currentWindowBounds = GetWindowBounds(currentWindowHandle);
            // Let's create the InteractorAgent.
            var interactorAgent = host.InitializeVirtualInteractorAgent(currentWindowHandle, "ConsoleWindowAgent");

            // Next we are going to create an interactor, which we will define with the gaze aware behavior.
            // Gaze aware behavior simply tells you whether somebody is looking at the interactor or not.
            interactorAgent
                .AddInteractorFor(currentWindowBounds)
                .WithGazeAware()
                .HasGaze(() => Console.WriteLine("Hey there!"))
                .LostGaze(() => Console.WriteLine("Bye..."));

            Console.ReadKey(true);

            // we will close the coonection to the Tobii Engine before exit.
            host.DisableConnection();
            */
            }

        #region Helpers 

            private static void PrintSampleIntroText()
            {
                Console.Clear();
                Console.WriteLine("============================================================");
                Console.WriteLine("|           Tobii Core SDK: Interactors                    |");
                Console.WriteLine("============================================================");

                Console.WriteLine();
                //Console.WriteLine("This sample will demonstrate you the usage of GazeAware interactors.");
                //Console.WriteLine("Look at the window to trigger HasGaze event and look away to trigger\n" +
                //                  "LostGaze event.");
                Console.WriteLine();
                //Console.WriteLine("HERE");
                Console.WriteLine("Server has started...");
            }

        private static Rectangle GetWindowBounds(IntPtr windowHandle)
        {
            NativeRect nativeNativeRect;
            if (GetWindowRect(windowHandle, out nativeNativeRect))
                return new Rectangle
                {
                    X = nativeNativeRect.Left,
                    Y = nativeNativeRect.Top,
                    Width = nativeNativeRect.Right,
                    Height = nativeNativeRect.Bottom
                };

            return new Rectangle(0d, 0d, 1000d, 1000d);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, out NativeRect nativeRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion
    }
}
