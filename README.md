# Process Hibernator

A high-performance Windows utility to hibernate background applications, saving RAM and CPU while keeping windows responsive.

## Architectural Structure

The project has been refactored into a modular architecture:

- **Logic**:
  - `ProcessManager.cs`: Handles process and thread suspension/resumption, and manages hibernated state.
  - `InteractionEngine.cs`: Manages global OS hooks and background monitoring for instant resumption.
- **Native**:
  - `NativeMethods.cs`: Contains all Win32 API declarations (P/Invokes) and related structs.
- **Models**:
  - `Models.cs`: Shared data models for process information and UI items.
- **UI**:
  - `MainForm.cs`: The primary user interface.
- **Entry Point**:
  - `Program.cs`: Application entry point and single-instance management.

## Key Features

- **Native UI Thread Exclusion**: Hibernates background processes while keeping their UI threads awake to prevent Windows Explorer hangs.
- **Surgical Resumption**: Instant resumption from Taskbar and Alt-Tab using kernel-level event hooks.
- **Eco-Pilot**: Automatic hibernation of idle background processes.
- **Multi-Selection**: Bulk management of applications.
- **Visual Feedback**: Real-time per-process countdown timers and system RAM history graph.
