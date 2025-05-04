using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace AceStreamStreamer
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource scanCancellation;
        private Dictionary<string, string> deviceMap = new Dictionary<string, string>();
        private Process ffmpegProcess;
        private Process aceProcess;

        private bool isFfmpegAvailable = false;
        private bool isAceAvailable = false;
        
        public MainWindow()
        {
            InitializeComponent();
            ScanButton.Click += ScanButton_Click;
            StopScanButton.Click += StopScanButton_Click;
            StartButton.Click += StartButton_Click;
            StopButton.Click += StopButton_Click;
            DeviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;

            CheckFfmpegPresence();
            CheckAceEnginePresence();
            UpdateStartButtonState();
        }

        private void CheckFfmpegPresence()
        {
            isFfmpegAvailable = File.Exists("ffmpeg.exe") || !string.IsNullOrEmpty(FindInPath("ffmpeg"));
            FfmpegStatusDot.Fill = isFfmpegAvailable ? Brushes.LimeGreen : Brushes.Red;
            UpdateStartButtonState();
        }

        private void CheckAceEnginePresence()
        {
            string acePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ACEStream", "engine", "ace_engine.exe");
            isAceAvailable = File.Exists(acePath);
            AceStatusDot.Fill = isAceAvailable ? Brushes.LimeGreen : Brushes.Red;
            UpdateStartButtonState();
        }

        private string GetAceEnginePath()
        {
            string userRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(userRoaming, "ACEStream", "engine", "ace_engine.exe");
        }

        private string FindInPath(string exe)
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';');
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, exe + ".exe");
                if (File.Exists(fullPath)) return fullPath;
            }
            return null;
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            DeviceComboBox.Items.Clear();
            deviceMap.Clear();
            scanCancellation = new CancellationTokenSource();

            string localIp = GetLocalIPAddress();
            if (string.IsNullOrEmpty(localIp))
            {
                AppendStatus("Could not determine local IP address.");
                return;
            }

            string subnet = string.Join(".", localIp.Split('.').Take(3));
            AppendStatus($"Scanning subnet: {subnet}.x");

            for (int i = 1; i <= 254; i++)
            {
                if (scanCancellation.Token.IsCancellationRequested) break;
                string ip = $"{subnet}.{i}";
                _ = Task.Run(() => PingHost(ip));
                ScanProgressBar.Value = i * 100 / 254;
                await Task.Delay(15);
            }

            AppendStatus("Scan complete.");
        }

        private void StopScanButton_Click(object sender, RoutedEventArgs e)
        {
            scanCancellation?.Cancel();
            AppendStatus("Scan cancelled.");
        }

        private void PingHost(string ip)
        {
            try
            {
                Ping ping = new Ping();
                var reply = ping.Send(ip, 500);
                if (reply.Status == IPStatus.Success)
                {
                    string hostname = Dns.GetHostEntry(ip).HostName;
                    Dispatcher.Invoke(() =>
                    {
                        deviceMap[hostname] = ip;
                        DeviceComboBox.Items.Add(hostname);
                    });
                }
            }
            catch { }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem == null || string.IsNullOrWhiteSpace(AceIdTextBox.Text))
            {
                AppendStatus("Please select device and enter AceStream ID.");
                return;
            }

            string ip = deviceMap[DeviceComboBox.SelectedItem.ToString()];
            string id = AceIdTextBox.Text.Trim();

            string args = $"-re -i http://127.0.0.1:6878/ace/getstream?id={id} -c copy -f mpegts udp://{ip}:1234";

            StartAceEngine();
            await Task.Delay(5000);

            ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            ffmpegProcess.Start();
            ffmpegProcess.BeginErrorReadLine();
            AppendStatus("FFmpeg streaming started.");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ffmpegProcess != null && !ffmpegProcess.HasExited)
                {
                    ffmpegProcess.Kill();
                    ffmpegProcess.Dispose();
                    ffmpegProcess = null;
                    AppendStatus("FFmpeg process stopped.");
                }

                if (aceProcess != null && !aceProcess.HasExited)
                {
                    aceProcess.Kill();
                    aceProcess.Dispose();
                    aceProcess = null;
                    AppendStatus("AceStream Engine stopped.");
                }
                else
                {
                    foreach (var proc in Process.GetProcessesByName("ace_engine"))
                    {
                        try
                        {
                            proc.Kill();
                            proc.Dispose();
                            AppendStatus("AceStream Engine stopped (fallback).");
                        }
                        catch (Exception ex)
                        {
                            AppendStatus($"Failed to stop AceStream fallback: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error during stopping: {ex.Message}");
            }
        }

        private void StartAceEngine()
        {
            try
            {
                string acePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ACEStream", "engine", "ace_engine.exe");

                if (!File.Exists(acePath))
                {
                    AppendStatus("AceStream Engine not found.");
                    return;
                }

                // Beende eventuell laufende Prozesse
                foreach (var proc in Process.GetProcessesByName("ace_engine"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                        AppendStatus("Existing AceEngine instance terminated.");
                    }
                    catch (Exception ex)
                    {
                        AppendStatus($"Failed to kill existing AceEngine: {ex.Message}");
                    }
                }

                Task.Delay(3000).Wait(); // Wartezeit von 3 Sekunden

                aceProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = acePath,
                        Arguments = "",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                aceProcess.Start();
                AppendStatus("AceStream Engine started.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error starting AceStream Engine: {ex.Message}");
            }
        }

        private void UpdateStartButtonState()
        {
            StartButton.IsEnabled = isFfmpegAvailable && isAceAvailable && DeviceComboBox.SelectedItem != null;
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeviceStatusDot.Fill = DeviceComboBox.SelectedItem != null ? Brushes.LimeGreen : Brushes.Red;
            UpdateStartButtonState();
        }

        private void AppendStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBox.AppendText($"[{DateTime.Now:T}] {message}\n");
                StatusTextBox.ScrollToEnd();
            });
        }

        private string GetLocalIPAddress()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = ni.GetIPProperties();
                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        return ua.Address.ToString();
                }
            }
            return null;
        }
    }
}