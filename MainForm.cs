using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WiFiDeviceScanner
{
    public partial class MainForm : Form
    {
        private ListView deviceListView;
        private Button scanButton;
        private Button refreshButton;
        private Label statusLabel;
        private ProgressBar progressBar;
        private BackgroundWorker scanWorker;
        private int _sortColumn = 0;
        private bool _sortAscending = true;

        public MainForm()
        {
            InitializeComponent();
            InitializeBackgroundWorker();
        }

        private void InitializeComponent()
        {
            this.Text = "WiFi Device Scanner";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(600, 400);

            // Create controls
            scanButton = new Button
            {
                Text = "Scan Network",
                Size = new Size(100, 30),
                Location = new Point(10, 10)
            };
            scanButton.Click += ScanButton_Click;

            refreshButton = new Button
            {
                Text = "Refresh",
                Size = new Size(80, 30),
                Location = new Point(120, 10)
            };
            refreshButton.Click += RefreshButton_Click;

            statusLabel = new Label
            {
                Text = "Ready to scan",
                Location = new Point(210, 15),
                Size = new Size(300, 20)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(10, 50),
                Size = new Size(760, 20),
                Visible = false
            };

            deviceListView = new ListView
            {
                Location = new Point(10, 80),
                Size = new Size(760, 480),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            deviceListView.Columns.Add("IP Address", 120);
            deviceListView.Columns.Add("MAC Address", 140);
            deviceListView.Columns.Add("Device Name", 200);
            deviceListView.Columns.Add("Status", 80);
            deviceListView.Columns.Add("Response Time", 100);

            // Add controls to form
            this.Controls.Add(scanButton);
            this.Controls.Add(refreshButton);
            this.Controls.Add(statusLabel);
            this.Controls.Add(progressBar);
            this.Controls.Add(deviceListView);

            // Handle resize and double click
            this.Resize += MainForm_Resize;
            deviceListView.DoubleClick += DeviceListView_DoubleClick;
            deviceListView.ColumnClick += DeviceListView_ColumnClick;
        }

        private void DeviceListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }
            deviceListView.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortAscending);
        }

        private void DeviceListView_DoubleClick(object sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
            {
                var selectedItem = deviceListView.SelectedItems[0];
                string ipAddress = selectedItem.SubItems[0].Text;
                string deviceName = selectedItem.SubItems[2].Text;
                
                var detailForm = new DeviceDetailForm(ipAddress, deviceName);
                detailForm.Show();
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                progressBar.Width = this.ClientSize.Width - 20;
                deviceListView.Width = this.ClientSize.Width - 20;
                deviceListView.Height = this.ClientSize.Height - 90;
            }
        }

        private void InitializeBackgroundWorker()
        {
            scanWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            scanWorker.DoWork += ScanWorker_DoWork;
            scanWorker.ProgressChanged += ScanWorker_ProgressChanged;
            scanWorker.RunWorkerCompleted += ScanWorker_RunWorkerCompleted;
        }

        private void ScanButton_Click(object sender, EventArgs e)
        {
            if (scanWorker.IsBusy)
            {
                scanWorker.CancelAsync();
                scanButton.Text = "Scan Network";
                statusLabel.Text = "Scan cancelled";
                progressBar.Visible = false;
            }
            else
            {
                StartScan();
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            if (!scanWorker.IsBusy)
            {
                StartScan();
            }
        }

        private void StartScan()
        {
            deviceListView.Items.Clear();
            scanButton.Text = "Cancel Scan";
            statusLabel.Text = "Scanning network...";
            progressBar.Visible = true;
            progressBar.Value = 0;
            scanWorker.RunWorkerAsync();
        }

        private void ScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var devices = new List<DeviceInfo>();

            // Get local network range
            string localIP = GetLocalIPAddress();
            if (string.IsNullOrEmpty(localIP))
            {
                e.Result = devices;
                return;
            }

            string baseIP = localIP.Substring(0, localIP.LastIndexOf('.') + 1);
            int totalIPs = 254;

            // Scan IP range (1-254) - Back to parallel for speed
            Parallel.For(1, 255, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                string ipToScan = baseIP + i;
                var device = PingDevice(ipToScan);
                
                if (device != null)
                {
                    lock (devices)
                    {
                        devices.Add(device);
                    }
                }

                int progress = (int)((double)i / totalIPs * 100);
                worker.ReportProgress(progress);
            });

            e.Result = devices;
        }

        private void ScanWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
            statusLabel.Text = $"Scanning network... {e.ProgressPercentage}%";
        }

        private void ScanWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            scanButton.Text = "Scan Network";
            progressBar.Visible = false;

            if (e.Cancelled)
            {
                statusLabel.Text = "Scan cancelled";
                return;
            }

            if (e.Error != null)
            {
                statusLabel.Text = "Error during scan: " + e.Error.Message;
                return;
            }

            var devices = e.Result as List<DeviceInfo>;
            
            // Sort devices by IP address numerically
            var sortedDevices = devices.OrderBy(d => {
                var parts = d.IPAddress.Split('.');
                return int.Parse(parts[0]) * 256 * 256 * 256 + 
                       int.Parse(parts[1]) * 256 * 256 + 
                       int.Parse(parts[2]) * 256 + 
                       int.Parse(parts[3]);
            });
            
            foreach (var device in sortedDevices)
            {
                var item = new ListViewItem(device.IPAddress);
                item.SubItems.Add(device.MACAddress);
                item.SubItems.Add(device.DeviceName);
                item.SubItems.Add(device.Status);
                item.SubItems.Add(device.ResponseTime);
                deviceListView.Items.Add(item);
            }

            statusLabel.Text = $"Scan complete. Found {devices.Count} devices.";
        }

        private DeviceInfo PingDevice(string ipAddress)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(ipAddress, 1000);
                    
                    if (reply.Status == IPStatus.Success)
                    {
                        var device = new DeviceInfo
                        {
                            IPAddress = ipAddress,
                            Status = "Online",
                            ResponseTime = reply.RoundtripTime + " ms"
                        };

                        // Try to get device name
                        try
                        {
                            var hostEntry = Dns.GetHostEntry(ipAddress);
                            device.DeviceName = hostEntry.HostName;
                        }
                        catch
                        {
                            device.DeviceName = "Unknown";
                        }

                        // Try to get MAC address
                        device.MACAddress = GetMACAddress(ipAddress);

                        return device;
                    }
                }
            }
            catch
            {
                // Ignore errors for individual IPs
            }

            return null;
        }

        private string GetLocalIPAddress()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetMACAddress(string ipAddress)
        {
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "arp";
                process.StartInfo.Arguments = "-a " + ipAddress;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Split('\n');
                foreach (string line in lines)
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && parts[0] == ipAddress)
                    {
                        return parts[1];
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return "Unknown";
        }
    }

    internal class ListViewItemComparer : IComparer
    {
        private readonly int _column;
        private readonly bool _ascending;

        public ListViewItemComparer(int column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }

        public int Compare(object? x, object? y)
        {
            string valX = ((ListViewItem)x!).SubItems[_column].Text;
            string valY = ((ListViewItem)y!).SubItems[_column].Text;

            int result = _column switch
            {
                0 => CompareIP(valX, valY),
                4 => CompareResponseTime(valX, valY),
                _ => string.Compare(valX, valY, StringComparison.OrdinalIgnoreCase)
            };

            return _ascending ? result : -result;
        }

        private static int CompareIP(string a, string b)
        {
            static long ToLong(string ip)
            {
                var p = ip.Split('.');
                if (p.Length != 4) return 0;
                return long.Parse(p[0]) * 16777216L + long.Parse(p[1]) * 65536L +
                       long.Parse(p[2]) * 256L + long.Parse(p[3]);
            }
            return ToLong(a).CompareTo(ToLong(b));
        }

        private static int CompareResponseTime(string a, string b)
        {
            int Parse(string s) => int.TryParse(s.Replace(" ms", ""), out int v) ? v : int.MaxValue;
            return Parse(a).CompareTo(Parse(b));
        }
    }

    public class DeviceInfo
    {
        public string IPAddress { get; set; }
        public string MACAddress { get; set; }
        public string DeviceName { get; set; }
        public string Status { get; set; }
        public string ResponseTime { get; set; }
    }

    public partial class DeviceDetailForm : Form
    {
        private string ipAddress;
        private string deviceName;
        private TextBox infoTextBox;
        private Button scanCommonPortsButton;
        private Button scanAllPortsButton;
        private Button closeButton;
        private ProgressBar scanProgressBar;
        private BackgroundWorker portScanWorker;

        public DeviceDetailForm(string ip, string name)
        {
            ipAddress = ip;
            deviceName = name;
            InitializeDetailForm();
            InitializePortScanWorker();
            LoadDeviceInfo();
        }

        private void InitializeDetailForm()
        {
            this.Text = $"Device Details - {ipAddress}";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;

            infoTextBox = new TextBox
            {
                Location = new Point(10, 10),
                Size = new Size(560, 300),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            scanCommonPortsButton = new Button
            {
                Text = "Scan Common Ports",
                Location = new Point(10, 320),
                Size = new Size(150, 30)
            };
            scanCommonPortsButton.Click += ScanCommonPorts_Click;

            scanAllPortsButton = new Button
            {
                Text = "Scan All Ports (1-65535)",
                Location = new Point(170, 320),
                Size = new Size(180, 30)
            };
            scanAllPortsButton.Click += ScanAllPorts_Click;

            closeButton = new Button
            {
                Text = "Close",
                Location = new Point(500, 320),
                Size = new Size(70, 30)
            };
            closeButton.Click += (s, e) => this.Close();

            scanProgressBar = new ProgressBar
            {
                Location = new Point(10, 360),
                Size = new Size(560, 20),
                Visible = false
            };

            this.Controls.Add(infoTextBox);
            this.Controls.Add(scanCommonPortsButton);
            this.Controls.Add(scanAllPortsButton);
            this.Controls.Add(closeButton);
            this.Controls.Add(scanProgressBar);
        }

        private void InitializePortScanWorker()
        {
            portScanWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            portScanWorker.DoWork += PortScanWorker_DoWork;
            portScanWorker.ProgressChanged += PortScanWorker_ProgressChanged;
            portScanWorker.RunWorkerCompleted += PortScanWorker_RunWorkerCompleted;
        }

        private void LoadDeviceInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"═══════════════════════════════════════");
            info.AppendLine($"      DEVICE INFORMATION");
            info.AppendLine($"═══════════════════════════════════════");
            info.AppendLine($"IP Address: {ipAddress}");
            info.AppendLine($"Device Name: {deviceName}");
            info.AppendLine();

            try
            {
                // Get more detailed DNS information
                var hostEntry = System.Net.Dns.GetHostEntry(ipAddress);
                info.AppendLine($"Full Hostname: {hostEntry.HostName}");
                if (hostEntry.Aliases.Length > 0)
                {
                    info.AppendLine($"Aliases: {string.Join(", ", hostEntry.Aliases)}");
                }
                info.AppendLine();
            }
            catch
            {
                info.AppendLine("Hostname: Unable to resolve");
                info.AppendLine();
            }

            // Get ping statistics
            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    info.AppendLine("PING STATISTICS:");
                    for (int i = 0; i < 4; i++)
                    {
                        var reply = ping.Send(ipAddress, 5000);
                        if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                        {
                            info.AppendLine($"Ping {i + 1}: {reply.RoundtripTime}ms");
                        }
                        else
                        {
                            info.AppendLine($"Ping {i + 1}: {reply.Status}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                info.AppendLine($"Ping Error: {ex.Message}");
            }

            info.AppendLine();
            info.AppendLine("═══════════════════════════════════════");
            info.AppendLine("Use buttons below to scan for open ports");
            info.AppendLine("═══════════════════════════════════════");

            infoTextBox.Text = info.ToString();
        }

        private void ScanCommonPorts_Click(object sender, EventArgs e)
        {
            if (portScanWorker.IsBusy) return;

            var commonPorts = new int[] { 21, 22, 23, 25, 53, 80, 110, 143, 443, 993, 995, 1433, 3389, 5432, 5900, 8080 };
            StartPortScan(commonPorts, "Common Ports");
        }

        private void ScanAllPorts_Click(object sender, EventArgs e)
        {
            if (portScanWorker.IsBusy) return;

            var result = MessageBox.Show(
                "Scanning all 65535 ports will take several minutes.\nAre you sure you want to continue?",
                "Full Port Scan",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                var allPorts = Enumerable.Range(1, 65535).ToArray();
                StartPortScan(allPorts, "All Ports (1-65535)");
            }
        }

        private void StartPortScan(int[] ports, string scanType)
        {
            scanCommonPortsButton.Enabled = false;
            scanAllPortsButton.Enabled = false;
            scanProgressBar.Visible = true;
            scanProgressBar.Value = 0;

            infoTextBox.AppendText($"\n\nStarting {scanType} scan...\n");
            infoTextBox.AppendText($"═══════════════════════════════════════\n");

            portScanWorker.RunWorkerAsync(ports);
        }

        private void PortScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var ports = e.Argument as int[];
            var openPorts = new List<(int port, string service)>();

            for (int i = 0; i < ports.Length; i++)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                int port = ports[i];
                if (IsPortOpen(ipAddress, port))
                {
                    string service = GetServiceName(port);
                    openPorts.Add((port, service));
                }

                int progress = (i + 1) * 100 / ports.Length;
                worker.ReportProgress(progress, openPorts.Count);
            }

            e.Result = openPorts;
        }

        private void PortScanWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            scanProgressBar.Value = e.ProgressPercentage;
        }

        private void PortScanWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            scanProgressBar.Visible = false;
            scanCommonPortsButton.Enabled = true;
            scanAllPortsButton.Enabled = true;

            if (e.Cancelled)
            {
                infoTextBox.AppendText("Port scan cancelled.\n");
                return;
            }

            if (e.Error != null)
            {
                infoTextBox.AppendText($"Error during port scan: {e.Error.Message}\n");
                return;
            }

            var openPorts = e.Result as List<(int port, string service)>;
            
            if (openPorts.Count == 0)
            {
                infoTextBox.AppendText("No open ports found.\n");
            }
            else
            {
                infoTextBox.AppendText($"Found {openPorts.Count} open ports:\n\n");
                foreach (var (port, service) in openPorts.OrderBy(p => p.port))
                {
                    infoTextBox.AppendText($"Port {port:D5}: {service}\n");
                }
            }

            infoTextBox.AppendText("\nScan completed.\n");
            infoTextBox.SelectionStart = infoTextBox.Text.Length;
            infoTextBox.ScrollToCaret();
        }

        private bool IsPortOpen(string hostname, int port)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var result = client.BeginConnect(hostname, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private string GetServiceName(int port)
        {
            var services = new Dictionary<int, string>
            {
                {21, "FTP"}, {22, "SSH"}, {23, "Telnet"}, {25, "SMTP"}, {53, "DNS"},
                {80, "HTTP"}, {110, "POP3"}, {143, "IMAP"}, {443, "HTTPS"}, {993, "IMAPS"},
                {995, "POP3S"}, {1433, "SQL Server"}, {3389, "RDP"}, {5432, "PostgreSQL"},
                {5900, "VNC"}, {8080, "HTTP Proxy"}, {135, "RPC"}, {139, "NetBIOS"}, 
                {445, "SMB"}, {1521, "Oracle"}, {3306, "MySQL"}, {5000, "UPnP"},
                {8443, "HTTPS Alt"}, {9090, "Web Admin"}, {2222, "SSH Alt"}, {8888, "HTTP Alt"}
            };

            return services.ContainsKey(port) ? services[port] : "Unknown Service";
        }
    }
}