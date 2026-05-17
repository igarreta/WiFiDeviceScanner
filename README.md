# WiFi Device Scanner

A Windows application to discover and analyze devices connected to your local network. Built with C# and Windows Forms.

![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

---

## Features

### Network Discovery
- **Auto-detect network range** — detects your active adapter and scans the /24 subnet automatically
- **Adapter range selector** — combo box lists all active network adapters with their computed IP ranges; select one to scan that segment directly
- **Custom IP range** — type any IPv4 range (e.g. `192.168.1.10-192.168.1.100`); recent ranges are remembered across sessions
- **Last selection remembered** — the network selector restores your last used choice on next launch
- **Parallel scanning** — 50 concurrent pings for fast discovery (254 IPs in ~3 seconds)
- **Accurate MAC resolution** — a single `arp -a` pass runs after all pings complete, so the ARP cache is fully populated before MAC lookup
- **Local machine recognition** — the scanner's own IP is detected and labelled `(This machine)` with the real adapter MAC
- **Routed device handling** — devices on a different L2 segment (behind a VLAN or router) show `N/A (routed)` instead of the misleading "Unknown"
- **Device name resolution** — DNS reverse lookup with NetBIOS (`nbtstat`) fallback for hosts that don't respond to DNS

### List View
- **Columns**: IP Address · MAC Address · Device Name · Status · Response Time · Not Seen For
- **Click any column header to sort** — IP and Response Time sort numerically; Not Seen For sorts by elapsed duration; online devices always appear above offline ones regardless of sort direction
- **Offline device history** — devices seen in previous scans but not responding now appear in gray at the bottom with a "Not Seen For" duration (e.g. `5 min`, `2h 30m`, `3d 4h`)

### Scan vs. Refresh
| Action | Behavior |
|---|---|
| **Scan Network** | Clears the list and rebuilds from scratch. Appends offline history rows at the bottom. |
| **Refresh** | Overlays new results onto the existing list without clearing. Rows that disappear turn gray in-place. Newly found devices are appended. |

### Device Management
- **Custom names** — right-click any device → **Rename…** to assign a friendly label (e.g. "Living Room TV"). Names are saved by MAC address and survive IP changes.
- **Clear name** — right-click → **Clear custom name** reverts to the DNS/NetBIOS hostname.
- **Double-click** any row to open the Device Details window.

### Export
- Click **Export…** to save the current list as a CSV file.
- Choose **All devices** (includes offline history rows) or **Connected only**.
- CSV columns: `IP Address, MAC Address, Device Name, Status, Response Time, Not Seen For`

### Device Details Window
- Full hostname and DNS aliases
- 4-ping statistics
- Port scanner: **Common Ports** (quick, ~16 well-known ports) or **All Ports 1–65535** (thorough, takes several minutes)
- Open ports listed with service names (HTTP, SSH, RDP, etc.)

---

## Quick Start

### Prerequisites
- Windows 10 / 11
- .NET 8.0 SDK or Runtime

### Build and run
```bash
git clone https://github.com/igarreta/WifiDeviceScanner.git
cd WifiDeviceScanner
dotnet run
```

Or open in Visual Studio 2022 and press F5.

---

## How to Use

### Scanning
1. Launch the application.
2. The **Network** combo box defaults to `Auto (detect)`. You can also select an adapter range (e.g. `192.168.1.1-192.168.1.254 (Wi-Fi)`) or type a custom range.
3. Click **Scan Network** for a full fresh scan, or **Refresh** to update the existing list in-place.
4. The progress bar fills as each IP is probed. Results appear after the scan completes.

### Understanding MAC values
| MAC column value | Meaning |
|---|---|
| `AA:BB:CC:DD:EE:FF` | Normal — device is on the local L2 segment |
| `(This machine)` in Name, real MAC shown | The scanner's own IP |
| `N/A (routed)` | Device routes through the gateway; its MAC is not visible on this segment |

### Offline devices
Devices that appeared in a previous scan but are not responding now are shown in gray at the bottom of the list. The **Not Seen For** column shows how long ago they were last seen. These rows are removed from the list only when you do a fresh **Scan Network** and the device has never been seen before.

### Naming devices
1. Right-click any row → **Rename…**
2. Enter a friendly name and click OK.
3. The name is saved to `%AppData%\WiFiDeviceScanner\config.json` keyed by MAC address, so it persists across sessions even if the device's IP changes.

### Exporting
1. Click **Export…**
2. Choose **All devices** or **Connected only**.
3. Click **Save** and pick a file location. The CSV includes all visible columns.

---

## Technical Details

### Architecture
- **Framework**: .NET 8.0 · Windows Forms
- **Language**: C# 12
- **Threading**: `BackgroundWorker` keeps the UI responsive during scans
- **MAC resolution**: Single `arp -a` call after all parallel pings complete; no per-IP ARP races
- **Local machine MAC**: Read directly from `NetworkInterface.GetPhysicalAddress()` — not from ARP
- **Persistence**: `System.Text.Json` → `%AppData%\WiFiDeviceScanner\config.json`

### Scan algorithm
1. Enumerate local adapters to find the network range (or use user-supplied range).
2. `Parallel.ForEach` with 50 concurrent pings across all IPs.
3. After all pings: one `arp -a` call parses the full ARP table.
4. For each responding IP: match to ARP table → assign MAC. If the IP is the local machine's own address → read MAC from adapter. If no ARP entry → mark `N/A (routed)`.
5. DNS reverse lookup (+ NetBIOS fallback) for device names.
6. Merge results into the list view; persist `LastSeen` / `KnownIPs` to config.

### Persistence (`config.json`)
| Key | Contents |
|---|---|
| `DeviceNames` | `{ "MAC": "Custom name" }` |
| `RecentNetworks` | Last 10 manually entered IP ranges |
| `LastNetworkSelection` | Restores combo box on next launch |
| `LastSeen` | `{ "MAC": ISO-8601 timestamp }` for offline history |
| `KnownIPs` | `{ "MAC": "last known IP" }` for offline rows |

---

## Troubleshooting

**No devices found**
- Ensure the machine is connected to the network.
- Check that Windows Firewall allows the app to make outbound ICMP requests.
- Some devices block ping (IoT devices, hardened servers).

**MAC shows `N/A (routed)`**
- The device is on a different network segment (separate VLAN, guest network, or different radio band on the router). Its MAC is not visible from this machine — this is a network topology limitation, not a bug.

**Device name shows "Unknown"**
- DNS reverse lookup failed and NetBIOS did not respond. Assign a custom name via right-click → **Rename…**

**Slow scan**
- Reduce the range to only the IPs you care about (e.g. `192.168.1.1-192.168.1.50`).
- Full port scans (1–65535) on the Device Details window take several minutes by design.

---

## Changelog

### Version 1.4
- Local machine detection: shows real adapter MAC and `(This machine)` label
- Routed device detection: shows `N/A (routed)` for cross-segment devices instead of "Unknown"
- Fixed duplicate rows on Refresh for devices with invalid MACs
- Fixed network selector crash when "Auto (detect)" was selected (annotation stripping now occurs after the auto-detect check)
- Network selection remembered across sessions

### Version 1.3
- Single ARP pass for accurate MAC resolution (eliminated 50-parallel-ARP race condition)
- NetBIOS name fallback (`nbtstat`) when DNS fails
- Network adapter ranges shown in the network selector combo box
- Scan vs. Refresh distinction (Scan rebuilds; Refresh merges in-place)
- Offline device history with "Not Seen For" column
- Export to CSV (all devices or connected only)
- Port scan output one line per port (fixed `\r\n` rendering in WinForms TextBox)
- Column sorting with online devices always grouped above offline

### Version 1.2
- Custom IP range scanning with recent ranges remembered
- Persistent device names saved by MAC address

### Version 1.1
- Device detail window with DNS, ping statistics, and port scanning
- Improved IP address sorting

### Version 1.0
- Initial release: network scan, IP/MAC/hostname display, progress bar

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

> **Disclaimer**: Use this tool only on networks you own or have explicit permission to scan.
