# WiFi Device Scanner

A powerful Windows application to discover and analyze devices connected to your WiFi network. Built with C# and Windows Forms, this tool provides comprehensive network scanning capabilities with an intuitive graphical interface.

![WiFi Device Scanner](https://img.shields.io/badge/Platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

## 🌟 Features

### Network Discovery
- **Fast Network Scanning**: Automatically detects your local network range and scans all possible IP addresses
- **Custom IP Range**: Scan any IPv4 range (e.g. `192.168.1.10-192.168.1.100`) in addition to the auto-detected network. Recently used ranges are remembered.
- **Device Information**: Shows IP address, MAC address, device name, online status, and response time
- **Custom Device Names**: Right-click a device to rename it. Names are saved by MAC address and persist across sessions.
- **Smart Sorting**: Results are automatically sorted by IP address for easy navigation
- **Real-time Progress**: Visual progress bar shows scanning progress
- **Parallel Processing**: Uses multi-threading for fast network discovery

### Device Analysis
- **Detailed Device Information**: Double-click any device to view comprehensive details
- **DNS Resolution**: Full hostname resolution with aliases
- **Ping Statistics**: Multiple ping tests to assess device responsiveness
- **Port Scanning**: Two scanning modes for security analysis

### Port Scanning Capabilities
- **Common Ports Scan**: Quick scan of frequently used ports (HTTP, HTTPS, SSH, FTP, etc.)
- **Full Port Scan**: Complete scan of all 65,535 ports (1-65535)
- **Service Detection**: Automatically identifies known services running on open ports
- **Progress Tracking**: Real-time progress indication during port scanning

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK or later

### Installation Options

#### Option 1: Using Visual Studio
1. Install [Visual Studio Community 2022](https://visualstudio.microsoft.com/downloads/) (free)
2. Select ".NET desktop development" workload during installation
3. Clone or download this repository
4. Open the solution in Visual Studio
5. Press F5 to build and run

#### Option 2: Using VS Code or Command Line
1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
2. Clone this repository:
   ```bash
   git clone https://github.com/igarreta/WifiDeviceScanner.git
   cd WifiDeviceScanner
   ```
3. Build and run:
   ```bash
   dotnet build
   dotnet run
   ```

## 📖 How to Use

### Basic Network Scanning
1. Launch the application
2. Leave the **Network** selector on "Auto (detect)" or type/select a range like `192.168.1.10-192.168.1.100`
3. Click **"Scan Network"** to start discovering devices
4. Wait for the scan to complete (typically 10-30 seconds)
5. View all discovered devices in the list

### Naming Devices
1. Right-click any device row
2. Choose **"Rename..."** and enter a friendly name (e.g. "Living Room TV")
3. The name is saved by MAC address to `%AppData%/WiFiDeviceScanner/config.json` and reappears in future scans even if the IP changes
4. Use **"Clear custom name"** to revert to the DNS hostname

### Device Analysis
1. **Double-click** on any device in the list
2. A detailed window opens showing:
   - Complete device information
   - DNS resolution details
   - Ping statistics (4 test pings)
   - Port scanning options

### Port Scanning
- **Common Ports**: Click "Scan Common Ports" for a quick security check
- **Full Scan**: Click "Scan All Ports" for comprehensive analysis (takes several minutes)

## 🖥️ Screenshots

### Main Interface
```
┌─────────────────────────────────────────────────────────┐
│ WiFi Device Scanner                                  [X]│
├─────────────────────────────────────────────────────────┤
│ [Scan Network] [Refresh]  Status: Ready to scan        │
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ 100%             │
│                                                         │
│ IP Address  │MAC Address    │Device Name        │Status │
│ 192.168.1.1 │a1:b2:c3:d4:e5 │Router            │Online │
│ 192.168.1.10│f6:g7:h8:i9:j0 │Desktop-PC        │Online │
│ 192.168.1.15│k1:l2:m3:n4:o5 │iPhone-12         │Online │
│             │               │                   │       │
└─────────────────────────────────────────────────────────┘
```

### Device Details Window
```
┌─────────────────────────────────────────────────────────┐
│ Device Details - 192.168.1.10                      [X]│
├─────────────────────────────────────────────────────────┤
│ ═══════════════════════════════════════               │
│       DEVICE INFORMATION                               │
│ ═══════════════════════════════════════               │
│ IP Address: 192.168.1.10                              │
│ Device Name: Desktop-PC                                │
│ Full Hostname: desktop-pc.local                        │
│                                                         │
│ PING STATISTICS:                                        │
│ Ping 1: 2ms                                           │
│ Ping 2: 1ms                                           │
│ Ping 3: 3ms                                           │
│ Ping 4: 2ms                                           │
│                                                         │
│ [Scan Common Ports] [Scan All Ports] [Close]          │
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ 65%                   │
└─────────────────────────────────────────────────────────┘
```

## 🔧 Technical Details

### Architecture
- **Framework**: .NET 8.0 with Windows Forms
- **Language**: C# 12
- **Threading**: BackgroundWorker for non-blocking operations
- **Network**: System.Net.NetworkInformation for ping operations
- **Sockets**: TcpClient for port scanning

### Key Components
- `MainForm.cs`: Main application window and network scanning logic
- `DeviceDetailForm.cs`: Device analysis and port scanning interface
- `DeviceInfo.cs`: Data model for network device information

### Network Scanning Algorithm
1. Detects local IP address and network range
2. Performs parallel ping operations on IP range (typically 192.168.1.1-254)
3. Resolves hostnames using DNS lookup
4. Retrieves MAC addresses using ARP table
5. Sorts results by IP address numerically

### Port Scanning
- **Timeout**: 100ms per port for responsive scanning
- **Common Ports**: Pre-defined list of 16 most common services
- **Service Detection**: Built-in database of port-to-service mappings
- **Parallel Processing**: Optimized for speed while maintaining accuracy

## 🛡️ Security & Privacy

- **Local Network Only**: Scans only your local network segment
- **No Data Collection**: No information is sent outside your network
- **Read-Only Operations**: Only performs network discovery, no modifications
- **Firewall Friendly**: Uses standard ICMP ping and TCP connect operations

## 🐛 Troubleshooting

### Common Issues

**"No devices found"**
- Ensure you're connected to WiFi
- Check if your firewall is blocking the application
- Some devices may not respond to ping requests

**"Permission denied errors"**
- Run as Administrator for full functionality
- Some antivirus software may block network scanning

**"Slow scanning"**
- Large networks (with many devices) take longer to scan
- Cancel and restart if scanning appears stuck

### Performance Tips
- Use "Common Ports" scan for quick analysis
- Close other network-intensive applications during full port scans
- Consider excluding inactive IP ranges for faster scanning

## 📝 Changelog

### Version 1.0
- Initial release with basic network scanning
- Device discovery with IP, MAC, and hostname resolution
- Real-time progress indication

### Version 1.1
- Added device detail analysis window
- Implemented port scanning capabilities
- Enhanced DNS resolution with ping statistics
- Improved IP address sorting algorithm

## 🤝 Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

### Development Setup
1. Fork the repository
2. Create a feature branch: `git checkout -b feature-name`
3. Make your changes and test thoroughly
4. Submit a pull request with a clear description

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with love using C# and Windows Forms
- Inspired by network administration tools like Nmap and Advanced IP Scanner
- Thanks to the .NET community for excellent documentation and examples

---

**⚠️ Disclaimer**: This tool is intended for network administration and security analysis of networks you own or have permission to scan. Always ensure you have proper authorization before scanning networks.