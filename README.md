# AceToTV

**Created by:** Brooks_EU  
**Year:** 2025  
**License:** MIT

## Description

AceToTV is a lightweight Windows application that allows you to stream AceStream sources directly to DLNA-compatible devices like Smart TVs, Android TV boxes, or gaming consoles using UDP via FFmpeg.  
The application detects devices in your local network, allows selection, and handles streaming configuration with minimal setup.

---

## Prerequisites

Before running AceToTV, please ensure the following components are installed or placed in the same folder as the executable:

- **.NET Framework 4.8**  
  Download: [https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)

- **AceStream Engine for Windows**  
  Download: [https://acestream.org/downloads/windows](https://acestream.org/downloads/windows)

- **FFmpeg**  
  Download (Windows build): [https://www.gyan.dev/ffmpeg/builds/](https://www.gyan.dev/ffmpeg/builds/)  
  → Place `ffmpeg.exe` in the same folder as AceToTV.exe or ensure it is in your system `PATH`.

---

## Usage

1. Click **"Scan"** to search for supported devices on your network.
2. Select a target device from the dropdown.
3. Enter a valid **AceStream ID**.
4. Click **"Start Stream"** to begin.
5. Use **"Stop Stream"** to terminate the stream and background processes.

---

## License

This project is licensed under the MIT License.
