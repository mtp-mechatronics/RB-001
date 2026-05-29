/**
 * @file SerialDataBridge.cs
 * @brief High-Throughput Asynchronous Serial Data Ingestion & Telemetry Pipeline
 * @note Windows Forms interface elements, databases, and macro playback algorithms are omitted.
 */

using System;
using System.IO.Ports;
using System.Threading;

namespace RobotControl.Core
{
    public class SerialDataBridge
    {
        private SerialPort _serialPort;
        private bool _isProcessing = false;
        private DateTime _recordingStartTime;

        public void InitializeSerialPipeline(string targetComPort)
        {
            _serialPort = new SerialPort
            {
                PortName = targetComPort,
                BaudRate = 115200,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = 8,
                Handshake = Handshake.None
            };

            // Bind Asynchronous Event Handler for High-Frequency Ingestion
            _serialPort.DataReceived += OnSerialDataReceived;
            _serialPort.Open();
            _recordingStartTime = DateTime.Now;
            _isProcessing = true;
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_isProcessing || _serialPort == null || !_serialPort.IsOpen)
                return;

            try
            {
                string incomingDataStream = _serialPort.ReadLine().Trim();
                if (!string.IsNullOrEmpty(incomingDataStream))
                {
                    // In production, this decouples into the Main UI Thread via BeginInvoke
                    ParseTelemetryData(incomingDataStream);
                }
            }
            catch (System.IO.IOException) { /* Handle hardware disconnect threads safely */ }
        }

        private void ParseTelemetryData(string rawPackage)
        {
            // Validate incoming telemetry frame schema (Expects: "B:", "S:", "E:", "W:")
            if (!rawPackage.Contains("B:") || !rawPackage.Contains("S:") || !rawPackage.Contains("E:") || !rawPackage.Contains("W:"))
                return;

            string[] telemetryTokens = rawPackage.Split(',');
            TimeSpan sessionTimestamp = DateTime.Now - _recordingStartTime;

            // Microsecond-accurate double serialization sample for telemetry profiles
            double capturedMilliseconds = sessionTimestamp.TotalMilliseconds;

            foreach (string token in telemetryTokens)
            {
                string[] keyValuePair = token.Split(':');
                if (keyValuePair.Length == 2)
                {
                    string jointIdentifier = keyValuePair[0].Trim();
                    if (int.TryParse(keyValuePair[1].Trim(), out int rawAdcValue))
                    {
                        // Signal Processing: Transforming 12-bit ADC data (0-4095) to Angular Domain (0-180)
                        int calibratedServoAngle = (int)((rawAdcValue / 4095.0) * 180.0);
                        
                        // Proceed to update asynchronous UI buffers or push into log arrays
                        Console.WriteLine($"[TS: {capturedMilliseconds}ms] Joint {jointIdentifier} -> Scaled Angle: {calibratedServoAngle}°");
                    }
                }
            }
        }

        public void TeardownPipeline()
        {
            _isProcessing = false;
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= OnSerialDataReceived;
                    _serialPort.Close();
                }
                _serialPort.Dispose();
            }
        }
    }
}