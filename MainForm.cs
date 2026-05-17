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
        private Label networkLabel;
        private ComboBox networkComboBox;
        private ProgressBar progressBar;
        private BackgroundWorker scanWorker;
        private Button exportButton;
        private ContextMenuStrip deviceContextMenu;
        private ToolStripMenuItem renameMenuItem;
        private ToolStripMenuItem clearNameMenuItem;
        private ToolStripMenuItem copyIpMenuItem;
        private ToolStripMenuItem copyMacMenuItem;
        private AppConfig config;
        private int _sortColumn = 0;
        private bool _sortAscending = true;
        private const string AutoNetworkLabel = "Auto (detect)";
        private const int MaxRangeSize = 4096;

        public MainForm()
        {
            config = ConfigStore.Load();
            InitializeComponent();
            InitializeBackgroundWorker();
        }

        private void InitializeComponent()
        {
            this.Text = "WiFi Device Scanner";
            this.Size = new Size(980, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(760, 400);

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

            exportButton = new Button
            {
                Text = "Export...",
                Size = new Size(90, 30),
                Location = new Point(210, 10)
            };
            exportButton.Click += ExportButton_Click;

            statusLabel = new Label
            {
                Text = "Ready to scan",
                Location = new Point(310, 15),
                Size = new Size(650, 20),
                AutoEllipsis = true
            };

            networkLabel = new Label
            {
                Text = "Network:",
                Location = new Point(10, 55),
                Size = new Size(60, 20)
            };

            networkComboBox = new ComboBox
            {
                Location = new Point(75, 52),
                Size = new Size(280, 25),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            RefreshNetworkComboBox();

            progressBar = new ProgressBar
            {
                Location = new Point(365, 55),
                Size = new Size(405, 20),
                Visible = false
            };

            deviceListView = new ListView
            {
                Location = new Point(10, 90),
                Size = new Size(950, 470),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            deviceListView.Columns.Add("IP Address", 120);
            deviceListView.Columns.Add("MAC Address", 140);
            deviceListView.Columns.Add("Device Name", 200);
            deviceListView.Columns.Add("Response Time", 100);
            deviceListView.Columns.Add("Not Seen For", 110);

            // Context menu for renaming
            deviceContextMenu = new ContextMenuStrip();
            renameMenuItem = new ToolStripMenuItem("Rename...");
            renameMenuItem.Click += RenameMenuItem_Click;
            clearNameMenuItem = new ToolStripMenuItem("Clear custom name");
            clearNameMenuItem.Click += ClearNameMenuItem_Click;
            copyIpMenuItem = new ToolStripMenuItem("Copy IP address");
            copyIpMenuItem.Click += CopyIpMenuItem_Click;
            copyMacMenuItem = new ToolStripMenuItem("Copy MAC address");
            copyMacMenuItem.Click += CopyMacMenuItem_Click;
            deviceContextMenu.Items.Add(renameMenuItem);
            deviceContextMenu.Items.Add(clearNameMenuItem);
            deviceContextMenu.Items.Add(new ToolStripSeparator());
            deviceContextMenu.Items.Add(copyIpMenuItem);
            deviceContextMenu.Items.Add(copyMacMenuItem);
            deviceContextMenu.Opening += DeviceContextMenu_Opening;
            deviceListView.ContextMenuStrip = deviceContextMenu;

            // Add controls to form
            this.Controls.Add(scanButton);
            this.Controls.Add(refreshButton);
            this.Controls.Add(exportButton);
            this.Controls.Add(statusLabel);
            this.Controls.Add(networkLabel);
            this.Controls.Add(networkComboBox);
            this.Controls.Add(progressBar);
            this.Controls.Add(deviceListView);

            // Handle resize and double click
            this.Resize += MainForm_Resize;
            deviceListView.DoubleClick += DeviceListView_DoubleClick;
            deviceListView.ColumnClick += DeviceListView_ColumnClick;
        }

        private void RefreshNetworkComboBox()
        {
            string current = networkComboBox.Text;
            if (string.IsNullOrEmpty(current))
                current = config.LastNetworkSelection;

            networkComboBox.BeginUpdate();
            networkComboBox.Items.Clear();
            networkComboBox.Items.Add(AutoNetworkLabel);
            foreach (var range in GetActiveAdapterRanges())
                networkComboBox.Items.Add(range);
            foreach (var entry in config.RecentNetworks)
                networkComboBox.Items.Add(entry);

            if (!string.IsNullOrEmpty(current))
            {
                if (networkComboBox.Items.Contains(current))
                    networkComboBox.SelectedItem = current;
                else
                    networkComboBox.Text = current;
            }
            else
                networkComboBox.SelectedIndex = 0;
            networkComboBox.EndUpdate();
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

        private void DeviceContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (deviceListView.SelectedItems.Count == 0)
            {
                e.Cancel = true;
                return;
            }

            var tag = deviceListView.SelectedItems[0].Tag as RowMeta;
            bool hasMac = tag != null && !string.IsNullOrEmpty(tag.Mac);
            renameMenuItem.Enabled = hasMac;
            clearNameMenuItem.Enabled = hasMac && config.DeviceNames.ContainsKey(tag!.Mac);
        }

        private void RenameMenuItem_Click(object? sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count == 0) return;
            var item = deviceListView.SelectedItems[0];
            var tag = item.Tag as RowMeta;
            if (tag == null || string.IsNullOrEmpty(tag.Mac)) return;

            string current = item.SubItems[2].Text;
            string? entered = PromptForName(current);
            if (entered == null) return;

            entered = entered.Trim();
            if (string.IsNullOrEmpty(entered))
                return;

            config.DeviceNames[tag.Mac] = entered;
            ConfigStore.Save(config);
            item.SubItems[2].Text = entered;
        }

        private void ClearNameMenuItem_Click(object? sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count == 0) return;
            var item = deviceListView.SelectedItems[0];
            var tag = item.Tag as RowMeta;
            if (tag == null || string.IsNullOrEmpty(tag.Mac)) return;

            if (!config.DeviceNames.Remove(tag.Mac)) return;
            ConfigStore.Save(config);
            item.SubItems[2].Text = string.IsNullOrEmpty(tag.DnsName) ? "Unknown" : tag.DnsName;
        }

        private void CopyIpMenuItem_Click(object? sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
                Clipboard.SetText(deviceListView.SelectedItems[0].SubItems[0].Text);
        }

        private void CopyMacMenuItem_Click(object? sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
                Clipboard.SetText(deviceListView.SelectedItems[0].SubItems[1].Text);
        }

        private string? PromptForName(string currentName)
        {
            using var dialog = new Form
            {
                Text = "Rename Device",
                Size = new Size(360, 140),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var label = new Label
            {
                Text = "Device name:",
                Location = new Point(12, 12),
                Size = new Size(320, 20)
            };
            var textBox = new TextBox
            {
                Text = currentName,
                Location = new Point(12, 35),
                Size = new Size(320, 25)
            };
            textBox.SelectAll();

            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(172, 70),
                Size = new Size(75, 25)
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(257, 70),
                Size = new Size(75, 25)
            };

            dialog.Controls.Add(label);
            dialog.Controls.Add(textBox);
            dialog.Controls.Add(okButton);
            dialog.Controls.Add(cancelButton);
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            return dialog.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                int formWidth = this.ClientSize.Width;
                statusLabel.Width = Math.Max(100, formWidth - statusLabel.Left - 10);
                progressBar.Width = Math.Max(100, formWidth - progressBar.Left - 10);
                deviceListView.Width = formWidth - 20;
                deviceListView.Height = this.ClientSize.Height - deviceListView.Top - 10;
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
                StartScan(isRefresh: true);
            }
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            bool? exportAll = PromptExportOptions();
            if (exportAll == null) return;

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"WiFiScan_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            ExportToCsv(sfd.FileName, exportAll.Value);
        }

        private bool? PromptExportOptions()
        {
            using var dialog = new Form
            {
                Text = "Export Options",
                Size = new Size(280, 160),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            };
            var rbAll    = new RadioButton { Text = "All devices (including offline)", Location = new Point(15, 15), Size = new Size(240, 22), Checked = true };
            var rbOnline = new RadioButton { Text = "Connected only",                 Location = new Point(15, 40), Size = new Size(240, 22) };
            var okButton     = new Button { Text = "Save",   DialogResult = DialogResult.OK,     Location = new Point(100, 85), Size = new Size(75, 25) };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(185, 85), Size = new Size(75, 25) };
            dialog.Controls.AddRange(new Control[] { rbAll, rbOnline, okButton, cancelButton });
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;
            return dialog.ShowDialog(this) == DialogResult.OK ? rbAll.Checked : null;
        }

        private void ExportToCsv(string path, bool allDevices)
        {
            try
            {
                using var writer = new System.IO.StreamWriter(path, false, System.Text.Encoding.UTF8);
                writer.WriteLine("IP Address,MAC Address,Device Name,Response Time,Not Seen For");

                string CsvField(string s)
                {
                    if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                        return "\"" + s.Replace("\"", "\"\"") + "\"";
                    return s;
                }

                foreach (ListViewItem item in deviceListView.Items)
                {
                    var meta = item.Tag as RowMeta;
                    if (!allDevices && !(meta?.IsOnline ?? true)) continue;

                    var fields = new[]
                    {
                        item.SubItems.Count > 0 ? item.SubItems[0].Text : "",
                        item.SubItems.Count > 1 ? item.SubItems[1].Text : "",
                        item.SubItems.Count > 2 ? item.SubItems[2].Text : "",
                        item.SubItems.Count > 3 ? item.SubItems[3].Text : "",
                        item.SubItems.Count > 4 ? item.SubItems[4].Text : ""
                    };
                    writer.WriteLine(string.Join(",", fields.Select(CsvField)));
                }

                MessageBox.Show(this, $"Exported {deviceListView.Items.Count} row(s) to:{Environment.NewLine}{path}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartScan(bool isRefresh = false)
        {
            string rawSelection = (networkComboBox.Text ?? string.Empty).Trim();
            config.LastNetworkSelection = rawSelection;

            ScanRequest request;

            if (string.IsNullOrEmpty(rawSelection) || rawSelection == AutoNetworkLabel)
            {
                request = new ScanRequest { IsAuto = true, IsRefresh = isRefresh };
            }
            else
            {
                // Strip adapter annotation suffix " (AdapterName)" — use IndexOf so nested parens in names don't break the range
                string selection = rawSelection;
                int parenIdx = selection.IndexOf(" (");
                if (parenIdx > 0)
                    selection = selection.Substring(0, parenIdx);

                if (!TryParseRange(selection, out var ips, out var error))
                {
                    MessageBox.Show(this, error, "Invalid network", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                request = new ScanRequest { IsAuto = false, RawInput = selection, Ips = ips, IsRefresh = isRefresh };
            }

            if (!isRefresh)
                deviceListView.Items.Clear();
            scanButton.Text = "Cancel Scan";
            statusLabel.Text = request.IsAuto ? "Scanning network..." : $"Scanning {request.Ips!.Count} addresses...";
            progressBar.Visible = true;
            progressBar.Value = 0;
            scanWorker.RunWorkerAsync(request);
        }

        private void ScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            var devices = new List<DeviceInfo>();
            var argument = e.Argument as ScanRequest ?? new ScanRequest { IsAuto = true };

            List<string> ipsToScan;
            if (argument.IsAuto)
            {
                string localIP = GetLocalIPAddress();
                if (string.IsNullOrEmpty(localIP))
                {
                    e.Result = new ScanResult { Devices = devices };
                    return;
                }
                string baseIP = localIP.Substring(0, localIP.LastIndexOf('.') + 1);
                ipsToScan = new List<string>(254);
                for (int i = 1; i <= 254; i++)
                    ipsToScan.Add(baseIP + i);
            }
            else
            {
                ipsToScan = argument.Ips!;
            }

            int totalIPs = ipsToScan.Count;
            int completed = 0;

            Parallel.ForEach(ipsToScan, new ParallelOptions { MaxDegreeOfParallelism = 50 }, ip =>
            {
                if (worker!.CancellationPending)
                    return;

                var device = PingDevice(ip);
                if (device != null)
                {
                    lock (devices)
                        devices.Add(device);
                }

                int done = System.Threading.Interlocked.Increment(ref completed);
                int progress = totalIPs == 0 ? 100 : (int)((double)done / totalIPs * 100);
                worker.ReportProgress(progress);
            });

            if (worker!.CancellationPending)
                e.Cancel = true;

            // Resolve MACs with a single arp -a call after all pings complete
            var arpTable = BuildArpTable();
            var localIPs = GetLocalIPAddresses();
            foreach (var device in devices)
            {
                if (localIPs.Contains(device.IPAddress))
                {
                    device.MACAddress = GetAdapterMacForIP(device.IPAddress) ?? "Unknown";
                    device.DeviceName = "(This machine)";
                }
                else if (device.MACAddress == "Unknown" || string.IsNullOrEmpty(device.MACAddress))
                {
                    if (arpTable.TryGetValue(device.IPAddress, out var mac))
                        device.MACAddress = mac;
                    else
                        device.MACAddress = "N/A (routed)";
                }
            }

            e.Result = new ScanResult { Devices = devices, NetworkEntry = argument.IsAuto ? null : argument.RawInput, IsRefresh = argument.IsRefresh };
        }

        public static bool TryParseRange(string input, out List<string> ips, out string error)
        {
            ips = new List<string>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Network is empty.";
                return false;
            }

            var parts = input.Trim().Split('-');
            if (parts.Length != 2)
            {
                error = "Expected format: A.B.C.D-A.B.C.D";
                return false;
            }

            if (!IPAddress.TryParse(parts[0].Trim(), out var startIp) ||
                !IPAddress.TryParse(parts[1].Trim(), out var endIp))
            {
                error = "One of the IP addresses is invalid.";
                return false;
            }

            if (startIp.AddressFamily != AddressFamily.InterNetwork ||
                endIp.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "Only IPv4 addresses are supported.";
                return false;
            }

            uint start = IpToUInt(startIp);
            uint end = IpToUInt(endIp);
            if (start > end)
            {
                error = "Start IP must be less than or equal to end IP.";
                return false;
            }

            long count = (long)end - start + 1;
            if (count > MaxRangeSize)
            {
                error = $"Range too large ({count} IPs). Maximum is {MaxRangeSize}.";
                return false;
            }

            for (uint v = start; v <= end; v++)
            {
                ips.Add(UIntToIp(v));
                if (v == uint.MaxValue) break;
            }
            return true;
        }

        private static uint IpToUInt(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static string UIntToIp(uint value)
        {
            return $"{(value >> 24) & 0xFF}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}";
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

            var result = e.Result as ScanResult;
            var devices = result?.Devices ?? new List<DeviceInfo>();
            DateTime now = DateTime.Now;

            if (result?.IsRefresh == true)
            {
                MergeIntoListView(devices, now);
            }
            else
            {
                // Full rebuild — list was already cleared in StartScan
                var sortedDevices = devices.OrderBy(d => {
                    var parts = d.IPAddress.Split('.');
                    return int.Parse(parts[0]) * 256 * 256 * 256 +
                           int.Parse(parts[1]) * 256 * 256 +
                           int.Parse(parts[2]) * 256 +
                           int.Parse(parts[3]);
                });

                var onlineMacs = new HashSet<string>();

                deviceListView.BeginUpdate();
                foreach (var device in sortedDevices)
                {
                    string normalizedMac = ConfigStore.NormalizeMac(device.MACAddress);
                    string dnsName = device.DeviceName ?? string.Empty;
                    string displayName = dnsName;
                    if (!string.IsNullOrEmpty(normalizedMac) &&
                        config.DeviceNames.TryGetValue(normalizedMac, out var customName) &&
                        !string.IsNullOrEmpty(customName))
                        displayName = customName;

                    if (IsValidMac(normalizedMac))
                    {
                        config.LastSeen[normalizedMac] = now;
                        config.KnownIPs[normalizedMac] = device.IPAddress;
                        onlineMacs.Add(normalizedMac);
                    }

                    var item = new ListViewItem(device.IPAddress);
                    item.SubItems.Add(device.MACAddress);
                    item.SubItems.Add(displayName);
                    item.SubItems.Add(device.ResponseTime);
                    item.SubItems.Add("Online");
                    item.Tag = new RowMeta { Mac = normalizedMac, DnsName = dnsName, IsOnline = true, LastSeenUtc = now };
                    deviceListView.Items.Add(item);
                }

                // Append known devices that didn't respond this scan
                foreach (var kvp in config.LastSeen.OrderByDescending(k => k.Value))
                {
                    string mac = kvp.Key;
                    if (onlineMacs.Contains(mac)) continue;

                    DateTime lastSeen = kvp.Value;
                    string knownIp = config.KnownIPs.TryGetValue(mac, out var ip) ? ip : "";
                    string displayName = config.DeviceNames.TryGetValue(mac, out var cn) ? cn : mac;

                    var offlineItem = new ListViewItem(knownIp);
                    offlineItem.SubItems.Add(mac);
                    offlineItem.SubItems.Add(displayName);
                    offlineItem.SubItems.Add(string.Empty);
                    offlineItem.SubItems.Add(FormatTimeSince(lastSeen));
                    offlineItem.ForeColor = SystemColors.GrayText;
                    offlineItem.Tag = new RowMeta { Mac = mac, DnsName = string.Empty, IsOnline = false, LastSeenUtc = lastSeen };
                    deviceListView.Items.Add(offlineItem);
                }
                deviceListView.EndUpdate();

                if (_sortColumn >= 0)
                    deviceListView.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortAscending);
            }

            if (result != null && !string.IsNullOrEmpty(result.NetworkEntry))
            {
                ConfigStore.AddRecentNetwork(config, result.NetworkEntry);
                RefreshNetworkComboBox();
                networkComboBox.SelectedItem = result.NetworkEntry;
            }

            ConfigStore.Save(config);
            statusLabel.Text = $"Scan complete. Found {devices.Count} device(s) online.";
        }

        private void MergeIntoListView(List<DeviceInfo> freshDevices, DateTime now)
        {
            var byMac = freshDevices
                .Where(d => IsValidMac(ConfigStore.NormalizeMac(d.MACAddress)))
                .GroupBy(d => ConfigStore.NormalizeMac(d.MACAddress))
                .ToDictionary(g => g.Key, g => g.First());
            var byIp = freshDevices.ToDictionary(d => d.IPAddress, d => d);
            var mergedMacs = new HashSet<string>();
            var mergedIps = new HashSet<string>();

            // Collect MACs already shown in the list so we don't duplicate offline rows
            var existingRowMacs = new HashSet<string>(
                deviceListView.Items.Cast<ListViewItem>()
                    .Select(it => (it.Tag as RowMeta)?.Mac ?? string.Empty)
                    .Where(m => !string.IsNullOrEmpty(m)));

            deviceListView.BeginUpdate();

            for (int i = 0; i < deviceListView.Items.Count; i++)
            {
                var item = deviceListView.Items[i];
                var meta = item.Tag as RowMeta;
                string mac = meta?.Mac ?? string.Empty;

                DeviceInfo? fresh = null;
                if (!string.IsNullOrEmpty(mac) && byMac.TryGetValue(mac, out var fd)) fresh = fd;
                else if (byIp.TryGetValue(item.SubItems[0].Text, out var fd2)) fresh = fd2;

                if (fresh != null)
                {
                    string normalizedMac = ConfigStore.NormalizeMac(fresh.MACAddress);
                    string dnsName = fresh.DeviceName ?? string.Empty;
                    string displayName = dnsName;
                    if (!string.IsNullOrEmpty(normalizedMac) &&
                        config.DeviceNames.TryGetValue(normalizedMac, out var cn) &&
                        !string.IsNullOrEmpty(cn))
                        displayName = cn;

                    item.SubItems[0].Text = fresh.IPAddress;
                    item.SubItems[1].Text = fresh.MACAddress;
                    item.SubItems[2].Text = displayName;
                    item.SubItems[3].Text = fresh.ResponseTime;
                    item.SubItems[4].Text = "Online";
                    item.ForeColor = SystemColors.WindowText;
                    item.Tag = new RowMeta { Mac = normalizedMac, DnsName = dnsName, IsOnline = true, LastSeenUtc = now };

                    if (IsValidMac(normalizedMac))
                    {
                        config.LastSeen[normalizedMac] = now;
                        config.KnownIPs[normalizedMac] = fresh.IPAddress;
                        mergedMacs.Add(normalizedMac);
                    }
                    mergedIps.Add(fresh.IPAddress);
                }
                else if (meta?.IsOnline == true)
                {
                    // Was online, now gone
                    DateTime lastSeen = meta.LastSeenUtc ?? now;
                    item.SubItems[3].Text = string.Empty;
                    item.SubItems[4].Text = FormatTimeSince(lastSeen);
                    item.ForeColor = SystemColors.GrayText;
                    item.Tag = new RowMeta { Mac = meta.Mac, DnsName = meta.DnsName, IsOnline = false, LastSeenUtc = lastSeen };
                }
                else if (meta?.IsOnline == false && meta.LastSeenUtc.HasValue)
                {
                    item.SubItems[4].Text = FormatTimeSince(meta.LastSeenUtc.Value);
                }
            }

            // Add newly discovered devices
            foreach (var fresh in freshDevices)
            {
                string normalizedMac = ConfigStore.NormalizeMac(fresh.MACAddress);
                if (IsValidMac(normalizedMac) && mergedMacs.Contains(normalizedMac)) continue;
                if (mergedIps.Contains(fresh.IPAddress)) continue;

                string dnsName = fresh.DeviceName ?? string.Empty;
                string displayName = dnsName;
                if (!string.IsNullOrEmpty(normalizedMac) &&
                    config.DeviceNames.TryGetValue(normalizedMac, out var cn) &&
                    !string.IsNullOrEmpty(cn))
                    displayName = cn;

                if (IsValidMac(normalizedMac))
                {
                    config.LastSeen[normalizedMac] = now;
                    config.KnownIPs[normalizedMac] = fresh.IPAddress;
                }

                var newItem = new ListViewItem(fresh.IPAddress);
                newItem.SubItems.Add(fresh.MACAddress);
                newItem.SubItems.Add(displayName);
                newItem.SubItems.Add(fresh.ResponseTime);
                newItem.SubItems.Add("Online");
                newItem.Tag = new RowMeta { Mac = normalizedMac, DnsName = dnsName, IsOnline = true, LastSeenUtc = now };
                deviceListView.Items.Add(newItem);
            }

            // Inject offline history for MACs not in current scan and not already displayed
            foreach (var kvp in config.LastSeen.OrderByDescending(k => k.Value))
            {
                string mac = kvp.Key;
                if (byMac.ContainsKey(mac)) continue;
                if (existingRowMacs.Contains(mac)) continue;

                DateTime lastSeen = kvp.Value;
                string knownIp = config.KnownIPs.TryGetValue(mac, out var ip) ? ip : "";
                string displayName = config.DeviceNames.TryGetValue(mac, out var cn) ? cn : mac;

                var offlineItem = new ListViewItem(knownIp);
                offlineItem.SubItems.Add(mac);
                offlineItem.SubItems.Add(displayName);
                offlineItem.SubItems.Add(string.Empty);
                offlineItem.SubItems.Add(FormatTimeSince(lastSeen));
                offlineItem.ForeColor = SystemColors.GrayText;
                offlineItem.Tag = new RowMeta { Mac = mac, DnsName = string.Empty, IsOnline = false, LastSeenUtc = lastSeen };
                deviceListView.Items.Add(offlineItem);
            }

            if (_sortColumn >= 0)
                deviceListView.ListViewItemSorter = new ListViewItemComparer(_sortColumn, _sortAscending);

            deviceListView.EndUpdate();
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
                            string? nbName = GetNetBiosName(ipAddress);
                            device.DeviceName = nbName ?? "Unknown";
                        }

                        // MAC address resolved after all pings complete via BuildArpTable
                        device.MACAddress = "Unknown";

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

        private static bool IsValidMac(string normalizedMac) =>
            normalizedMac.Length == 17 && normalizedMac.Count(c => c == ':') == 5;

        private static HashSet<string> GetLocalIPAddresses()
        {
            var result = new HashSet<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var uni in nic.GetIPProperties().UnicastAddresses)
                        if (uni.Address.AddressFamily == AddressFamily.InterNetwork)
                            result.Add(uni.Address.ToString());
                }
            }
            catch { }
            return result;
        }

        private static string? GetAdapterMacForIP(string ipAddress)
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var uni in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (uni.Address.AddressFamily == AddressFamily.InterNetwork &&
                            uni.Address.ToString() == ipAddress)
                        {
                            byte[] b = nic.GetPhysicalAddress().GetAddressBytes();
                            if (b.Length == 6)
                                return string.Join(":", b.Select(x => x.ToString("X2")));
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string FormatTimeSince(DateTime dt)
        {
            TimeSpan elapsed = DateTime.Now - dt;
            if (elapsed.TotalMinutes < 1) return "< 1 min";
            if (elapsed.TotalHours < 1)   return $"{(int)elapsed.TotalMinutes} min";
            if (elapsed.TotalDays < 1)    return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            return $"{(int)elapsed.TotalDays}d {elapsed.Hours}h";
        }

        private static List<string> GetActiveAdapterRanges()
        {
            var ranges = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    foreach (var uni in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (uni.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        byte[] addrBytes = uni.Address.GetAddressBytes();
                        byte[] maskBytes = uni.IPv4Mask.GetAddressBytes();

                        // If subnet is larger than /24, clamp to /24 to stay within MaxRangeSize
                        uint maskUint = ((uint)maskBytes[0] << 24) | ((uint)maskBytes[1] << 16) |
                                        ((uint)maskBytes[2] << 8) | maskBytes[3];
                        if (maskUint < 0xFFFFFF00u)
                            maskBytes = new byte[] { 255, 255, 255, 0 };

                        byte[] netBytes = new byte[4];
                        byte[] bcastBytes = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            netBytes[i] = (byte)(addrBytes[i] & maskBytes[i]);
                            bcastBytes[i] = (byte)(netBytes[i] | ~maskBytes[i]);
                        }
                        netBytes[3] += 1;
                        bcastBytes[3] -= 1;

                        string start = string.Join(".", netBytes);
                        string end = string.Join(".", bcastBytes);
                        if (start != end)
                            ranges.Add($"{start}-{end} ({nic.Name})");
                    }
                }
            }
            catch { }
            return ranges;
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

        private static string? GetNetBiosName(string ipAddress)
        {
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "nbtstat";
                process.StartInfo.Arguments = "-A " + ipAddress;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                bool exited = process.WaitForExit(2000);
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    return null;
                }
                string output = process.StandardOutput.ReadToEnd();
                foreach (string line in output.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Contains("<00>") && trimmed.Contains("UNIQUE"))
                    {
                        string name = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                        if (!string.IsNullOrWhiteSpace(name))
                            return name;
                    }
                }
            }
            catch { }
            return null;
        }

        private static Dictionary<string, string> BuildArpTable()
        {
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "arp";
                process.StartInfo.Arguments = "-a";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                foreach (string line in output.Split('\n'))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    string ip = parts[0];
                    string mac = parts[1];
                    if (!System.Net.IPAddress.TryParse(ip, out _)) continue;
                    if (ip.StartsWith("224.") || ip.StartsWith("239.") || ip.StartsWith("255.")) continue;
                    if (mac.Equals("ff-ff-ff-ff-ff-ff", StringComparison.OrdinalIgnoreCase)) continue;
                    if (mac.Contains('-') || mac.Contains(':'))
                        table[ip] = mac;
                }
            }
            catch { }
            return table;
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
            var itemX = (ListViewItem)x!;
            var itemY = (ListViewItem)y!;
            var metaX = itemX.Tag as RowMeta;
            var metaY = itemY.Tag as RowMeta;

            // Online devices always appear before offline, regardless of sort direction
            bool onlineX = metaX?.IsOnline ?? true;
            bool onlineY = metaY?.IsOnline ?? true;
            if (onlineX != onlineY)
                return onlineX ? -1 : 1;

            string valX = itemX.SubItems.Count > _column ? itemX.SubItems[_column].Text : string.Empty;
            string valY = itemY.SubItems.Count > _column ? itemY.SubItems[_column].Text : string.Empty;

            int result = _column switch
            {
                0 => CompareIP(valX, valY),
                3 => CompareResponseTime(valX, valY),
                4 => CompareNotSeenFor(metaX, metaY),
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

        private static int CompareNotSeenFor(RowMeta? a, RowMeta? b)
        {
            TimeSpan elapsedA = (a?.IsOnline ?? true) ? TimeSpan.Zero
                : (a!.LastSeenUtc.HasValue ? DateTime.Now - a.LastSeenUtc.Value : TimeSpan.MaxValue);
            TimeSpan elapsedB = (b?.IsOnline ?? true) ? TimeSpan.Zero
                : (b!.LastSeenUtc.HasValue ? DateTime.Now - b.LastSeenUtc.Value : TimeSpan.MaxValue);
            return elapsedA.CompareTo(elapsedB);
        }
    }

    public class DeviceInfo
    {
        public string IPAddress { get; set; } = string.Empty;
        public string MACAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ResponseTime { get; set; } = string.Empty;
    }

    internal class RowMeta
    {
        public string Mac { get; set; } = string.Empty;
        public string DnsName { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = true;
        public DateTime? LastSeenUtc { get; set; }
    }

    internal class ScanRequest
    {
        public bool IsAuto { get; set; }
        public bool IsRefresh { get; set; }
        public string? RawInput { get; set; }
        public List<string>? Ips { get; set; }
    }

    internal class ScanResult
    {
        public List<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
        public string? NetworkEntry { get; set; }
        public bool IsRefresh { get; set; }
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

            infoTextBox.AppendText($"{Environment.NewLine}{Environment.NewLine}Starting {scanType} scan...{Environment.NewLine}");
            infoTextBox.AppendText($"═══════════════════════════════════════{Environment.NewLine}");

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
                infoTextBox.AppendText($"Port scan cancelled.{Environment.NewLine}");
                return;
            }

            if (e.Error != null)
            {
                infoTextBox.AppendText($"Error during port scan: {e.Error.Message}{Environment.NewLine}");
                return;
            }

            var openPorts = e.Result as List<(int port, string service)>;
            
            if (openPorts.Count == 0)
            {
                infoTextBox.AppendText($"No open ports found.{Environment.NewLine}");
            }
            else
            {
                infoTextBox.AppendText($"Found {openPorts.Count} open ports:{Environment.NewLine}{Environment.NewLine}");
                foreach (var (port, service) in openPorts.OrderBy(p => p.port))
                {
                    infoTextBox.AppendText($"Port {port:D5}: {service}{Environment.NewLine}");
                }
            }

            infoTextBox.AppendText($"{Environment.NewLine}Scan completed.{Environment.NewLine}");
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