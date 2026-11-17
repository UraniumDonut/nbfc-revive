# Changelog

## 2.0.0-rc.1

### New
- The embedded controller is accessed through the [PawnIO](https://pawnio.eu) driver instead of WinRing0. WinRing0 has a known security vulnerability (CVE-2020-14979).
- CPU and GPU temperatures are read using LibreHardwareMonitorLib instead of OpenHardwareMonitor. This adds support for current gen CPUs.

### Fixed
- GPU temperatures were read from the CPU sensors.
- The CPU temperature plugin reported invalid temperatures instead of an error when no sensor was available.

### Third-party components
- [PawnIO](https://pawnio.eu) driver setup, GPL-2.0
- PawnIO LpcACPIEC module, LGPL-2.1
- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), MPL-2.0
