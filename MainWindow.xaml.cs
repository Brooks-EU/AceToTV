using System;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AceStreamStreamer
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource scanCancellation;
        private Dictionary<string, string> deviceMap = new Dictionary<string, string>();
        private Process ffmpegProcess;

        public MainWindow()
        {
            InitializeComponent();
            ScanButton.Click += ScanButton_Click;
            StopScanButton.Click += StopScanButton_Click;
            StartButton.Click += StartButton_Click;
            StopButton.Click += StopButton_Click;
            CheckFfmpegPresence();
        }

        private void CheckFfmpegPresence()
        {
            var found = File.Exists("ffmpeg.exe") || !string.IsNullOrEmpty(FindInPath("ffmpeg"));
            AppendStatus(found ? "FFmpeg found." : "FFmpeg not found. Please install or place ffmpeg.exe in app directory.");
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
            for (int i = 1; i <= 254; i++)
            {
                if (scanCancellation.Token.IsCancellationRequested) break;
                string ip = $"192.168.2.{i}";
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

            StartAceEngine();
            await Task.Delay(5000);
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
                    // Falls aceProcess null ist (weil extern gestartet), trotzdem beenden:
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

        private Process aceProcess;

        private void StartAceEngine()
        {
            try
            {
                // Prüfen, ob bereits eine Instanz läuft
                if (Process.GetProcessesByName("ace_engine").Length > 0)
                {
                    AppendStatus("AceStream Engine is already running.");
                    return;
                }

                // Pfad zur AceStream Engine ermitteln
                string userRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string acePath = Path.Combine(userRoaming, "ACEStream", "engine", "ace_engine.exe");

                if (!File.Exists(acePath))
                {
                    AppendStatus("AceStream Engine not found at expected location.");
                    return;
                }

                // Prozess starten
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

        private void AppendStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBox.AppendText($"[{DateTime.Now:T}] {message}\n");
                StatusTextBox.ScrollToEnd();
            });
        }


    }


}
