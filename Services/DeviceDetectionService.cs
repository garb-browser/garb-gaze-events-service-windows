using System;
using System.Collections.Generic;
using Interaction_Interactors_101.Providers;

// Try to use Tobii Consumer SDK
using Tobii.Interaction;

// Tobii Pro SDK (if available)
#if TOBII_PRO_SDK
using Tobii.Research;
#endif

namespace Interaction_Interactors_101.Services
{
    /// <summary>
    /// Service for detecting all connected eye tracker devices across both Consumer and Pro SDKs.
    /// </summary>
    public class DeviceDetectionService
    {
        /// <summary>
        /// Detect all connected eye tracker devices.
        /// Uses Pro SDK to enumerate physical devices (more reliable than Consumer SDK which caches).
        /// Returns devices from both Consumer SDK (Eye Tracker 5) and Pro SDK (Pro Nano, etc.)
        /// </summary>
        /// <returns>List of detected devices with their capabilities</returns>
        public List<DeviceInfo> DetectAllDevices()
        {
            var devices = new List<DeviceInfo>();
            bool foundConsumerHardware = false;

#if TOBII_PRO_SDK
            // Use Pro SDK to enumerate ALL physical devices (it actually checks USB connection)
            // This is more reliable than Consumer SDK which caches device info
            try
            {
                Console.WriteLine("[Detection] Enumerating physical devices via Pro SDK...");
                var eyeTrackers = EyeTrackingOperations.FindAllEyeTrackers();

                foreach (var tracker in eyeTrackers)
                {
                    string model = tracker.Model ?? "";

                    if (IsConsumerDevice(model))
                    {
                        // Consumer hardware (ET5, 4C) - add as Consumer type for Consumer SDK streaming
                        foundConsumerHardware = true;
                        devices.Add(new DeviceInfo
                        {
                            DeviceId = "tobii-consumer",
                            DeviceName = $"Tobii {GetFriendlyConsumerName(model)}",
                            DeviceType = "Consumer",
                            SerialNumber = tracker.SerialNumber,
                            FirmwareVersion = tracker.FirmwareVersion,
                            SamplingRate = 60,
                            SupportsPupilData = false,
                            SupportsGazeOrigin = false
                        });
                        Console.WriteLine($"[Detection] Found Consumer device: {GetFriendlyConsumerName(model)} (Serial: {tracker.SerialNumber})");
                    }
                    else
                    {
                        // Pro hardware - add as Pro type
                        string deviceName = model.StartsWith("Tobii Pro", StringComparison.OrdinalIgnoreCase)
                            ? model
                            : $"Tobii Pro {model}";

                        devices.Add(new DeviceInfo
                        {
                            DeviceId = tracker.Address.ToString(),
                            DeviceName = deviceName,
                            DeviceType = "Pro",
                            SerialNumber = tracker.SerialNumber,
                            FirmwareVersion = tracker.FirmwareVersion,
                            SamplingRate = GetSamplingRate(tracker),
                            SupportsPupilData = true,
                            SupportsGazeOrigin = true
                        });
                        Console.WriteLine($"[Detection] Found Pro device: {model} (Serial: {tracker.SerialNumber})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Detection] Pro SDK enumeration failed: " + ex.Message);
            }
#endif

#if !TOBII_PRO_SDK
            // Only use Consumer SDK detection when Pro SDK is NOT available
            // Pro SDK enumeration is authoritative - if it didn't find Consumer hardware,
            // the device is not physically connected (Consumer SDK caches and gives false positives)
            try
            {
                if (DetectConsumerDevice())
                {
                    devices.Add(new DeviceInfo
                    {
                        DeviceId = "tobii-consumer",
                        DeviceName = "Tobii Eye Tracker 5",
                        DeviceType = "Consumer",
                        SerialNumber = null,
                        FirmwareVersion = null,
                        SamplingRate = 60,
                        SupportsPupilData = false,
                        SupportsGazeOrigin = false
                    });
                    Console.WriteLine("[Detection] Found Consumer device (Pro SDK not available): Tobii Eye Tracker 5");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Detection] Consumer SDK detection failed: " + ex.Message);
            }
#endif

            Console.WriteLine($"[Detection] Total devices found: {devices.Count}");
            return devices;
        }

        /// <summary>
        /// Check if a Consumer device is available by attempting to initialize the Host.
        /// Requires Tobii Experience (or Tobii Core) software to be running.
        /// </summary>
        private bool DetectConsumerDevice()
        {
            Host host = null;
            try
            {
                Console.WriteLine("[Detection] Checking for Consumer device (requires Tobii Experience running)...");

                // Try to create a Host - this will throw if no Consumer device is available
                // or if Tobii Experience/Core software is not running
                host = new Host();

                // If we get here, a Consumer device is available
                // We can't get detailed device info from Consumer SDK, but we know it exists
                Console.WriteLine("[Detection] Consumer SDK Host initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                // No Consumer device available or Tobii Experience not running
                Console.WriteLine($"[Detection] Consumer SDK Host failed: {ex.Message}");
                Console.WriteLine("[Detection] Make sure Tobii Experience software is running for Eye Tracker 5");
                return false;
            }
            finally
            {
                // Clean up the Host connection
                if (host != null)
                {
                    try
                    {
                        host.DisableConnection();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Detect all Tobii Pro devices.
        /// Filters out Consumer devices (Eye Tracker 5, 4C) that may be enumerated by Pro SDK.
        /// </summary>
        private List<DeviceInfo> DetectProDevices()
        {
            var devices = new List<DeviceInfo>();

#if TOBII_PRO_SDK
            try
            {
                var eyeTrackers = EyeTrackingOperations.FindAllEyeTrackers();

                foreach (var tracker in eyeTrackers)
                {
                    string model = tracker.Model ?? "";

                    // Skip Consumer hardware (ET5, 4C, etc.) - they're already detected via Consumer SDK
                    // and don't work properly with Pro SDK for gaze streaming anyway
                    if (IsConsumerDevice(model))
                    {
                        Console.WriteLine($"[Detection] Skipping Consumer device in Pro SDK enumeration: {model}");
                        continue;
                    }

                    // Build device name - avoid "Tobii Pro Tobii Pro Nano" duplication
                    string deviceName = model.StartsWith("Tobii Pro", StringComparison.OrdinalIgnoreCase)
                        ? model
                        : $"Tobii Pro {model}";

                    devices.Add(new DeviceInfo
                    {
                        DeviceId = tracker.Address.ToString(),
                        DeviceName = deviceName,
                        DeviceType = "Pro",
                        SerialNumber = tracker.SerialNumber,
                        FirmwareVersion = tracker.FirmwareVersion,
                        SamplingRate = GetSamplingRate(tracker),
                        SupportsPupilData = true,
                        SupportsGazeOrigin = true
                    });

                    Console.WriteLine($"[Detection] Found Pro device: {model} (Serial: {tracker.SerialNumber})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Detection] Error enumerating Pro devices: " + ex.Message);
            }
#else
            Console.WriteLine("[Detection] Pro SDK not available. To enable, install Tobii.Research.x64 NuGet package and define TOBII_PRO_SDK.");
#endif

            return devices;
        }

        /// <summary>
        /// Check if a device model name indicates a Consumer device (not a true Pro device).
        /// Consumer devices can still be used via Pro SDK for basic gaze tracking.
        /// </summary>
        private bool IsConsumerDevice(string model)
        {
            if (string.IsNullOrEmpty(model))
                return false;

            string upperModel = model.ToUpperInvariant();

            // Eye Tracker 5 variants
            if (upperModel.Contains("IS5") || upperModel.Contains("EYETRACKER_5") || upperModel.Contains("EYETRACKER5"))
                return true;

            // Eye Tracker 4C variants
            if (upperModel.Contains("IS4") || upperModel.Contains("EYETRACKER_4") || upperModel.Contains("4C"))
                return true;

            // Other Consumer patterns (Tobii Gaming peripherals)
            if (upperModel.Contains("PERIPHERAL") && !upperModel.Contains("PRO"))
                return true;

            return false;
        }

        /// <summary>
        /// Convert internal model names to user-friendly names for Consumer devices.
        /// </summary>
        private string GetFriendlyConsumerName(string model)
        {
            if (string.IsNullOrEmpty(model))
                return "Eye Tracker";

            string upperModel = model.ToUpperInvariant();

            if (upperModel.Contains("IS5") || upperModel.Contains("EYETRACKER_5") || upperModel.Contains("EYETRACKER5"))
                return "Eye Tracker 5";

            if (upperModel.Contains("IS4") || upperModel.Contains("4C"))
                return "Eye Tracker 4C";

            // Default: return the model as-is
            return model;
        }

#if TOBII_PRO_SDK
        /// <summary>
        /// Get the sampling rate for a Pro device
        /// </summary>
        private double GetSamplingRate(IEyeTracker tracker)
        {
            try
            {
                // Get the current gaze output frequency
                return tracker.GetGazeOutputFrequency();
            }
            catch
            {
                // Default to 60Hz (Pro Nano rate)
                return 60;
            }
        }
#endif
    }
}
