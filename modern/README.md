# Experimental modern Windows build

Windows x64 tools for using and inspecting Apple MFi Bluetooth controllers, with SteelSeries Nimbus and Nimbus+ mapping profiles. Built with C#, WPF, and .NET 10.

This project modernizes the approach of [Axadiw/MFIGamepadFeeder](https://github.com/Axadiw/MFIGamepadFeeder).

## Choose an app

| App | Purpose | Virtual controller driver required? |
| --- | --- | --- |
| **MFI Gamepad Feeder** | Translate MFi input into a virtual Xbox 360 controller for compatible games. | Yes: ViGEmBus or ScpVBus. |
| **Nimbus Input Tester** | Inspect physical input with sticks, triggers, button history, and report diagnostics. | No. |

The tester does not create a virtual controller, install drivers, or replace the feeder for games that require Xbox-compatible input.

## Build and run

Requirements: Windows 10/11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

From the repository root, enter this directory before running the commands below:

```powershell
cd modern
```

This solution is separate from the legacy .NET Framework application at the root.

```powershell
dotnet build -c Release
dotnet test -c Release --no-build
```

### Test the physical Nimbus

1. Pair the controller in Windows Bluetooth settings and wake it.
2. Double-click **Nimbus Tester.cmd**, or run:

   ```powershell
   dotnet run --project src/MFIGamepadFeeder.Tester -c Release
   ```

3. Press buttons, move both sticks, and squeeze the triggers. Quick press/release events remain in **Button history** even when they occur between screen updates.
4. If no active input interface is found, check that Bluetooth says **Connected** and choose **Refresh devices**. A paired controller may be asleep.

The launcher builds the tester if its executable is missing. The direct view uses zero added deadzone and disables mapping combinations to expose the physical controls. The original Nimbus has no physical Back, L3, or R3 buttons.

### Enable Xbox-compatible output

The feeder requires a separately installed compatible virtual controller bus. It prefers ViGEmBus and can use an existing ScpVBus installation. Driver installers are not included, and the tester does not need either driver.

[ViGEmBus is retired](https://docs.nefarius.at/projects/ViGEm/End-of-Life/), and ScpVBus is a legacy driver. Driver availability and compatibility depend on the Windows configuration; this project does not maintain those drivers.

```powershell
dotnet run --project src/MFIGamepadFeeder.App -c Release
```

Select the Nimbus and its matching profile, then click **Start Feeding**. Keep the feeder running while using its virtual Xbox controller. An existing compatible driver does not need to be reinstalled just to use this update.

Default original Nimbus combinations:

- **LB + RB + Menu**: Back / View
- **LB + Menu**: Left stick click (L3)
- **RB + Menu**: Right stick click (R3)

## Compare physical and Xbox input

Keep the tester open, start feeding in the feeder app, and expand **Compare existing Xbox input** in the tester. Select a connected Xbox slot and compare its response with **Direct Nimbus input**.

This view only reads existing XInput controllers. Seeing both sources respond is expected during comparison and does not prove a game processes duplicate inputs. The feeder does not hide the physical controller from games; a game or another remapper may need configuration to consume only the intended source.

## What this update includes

- A standalone direct-HID Nimbus tester with automatic discovery and reconnect.
- Live stick, trigger, and button feedback that follows WPF's render cadence.
- Button edge history recorded independently of UI refreshes.
- Received-versus-decoded report counts, raw inspection, and report cadence.
- A lightweight visual-state snapshot and changed-field drawing to reduce avoidable display work; numeric diagnostics refresh separately at 10 Hz.
- Read-only comparison with existing Xbox input, with slower disconnected-slot probes.
- The feeder's mapping editor, configurable deadzone/pressure threshold, atomic output reports, and neutral output on disconnect.
- Automated tests covering parsing, packet construction, lifecycle, quick taps, unmapped reports, and changed-state timestamps.

## Measurement limits

**Report intervals** describe the cadence of reports read by the app. Controllers may send data only when controls change; an idle gap is not automatically input lag.

**HID receipt → UI update** measures scheduling for the latest changed decoded state after a HID read returns. It excludes Bluetooth delivery, pre-read buffering, display composition, and screen presentation. It is not a physical button-to-screen benchmark.

Native GameInput mapping has not been implemented or verified. Reading HID data directly does not by itself establish compatibility with a particular game. No measured game-latency improvement is claimed by this update.

## Project layout

- `src/MFIGamepadFeeder.App`: feeder and mapping editor.
- `src/MFIGamepadFeeder.Core`: HID input, mapping, backends, and test-session diagnostics.
- `src/MFIGamepadFeeder.Tester`: standalone WPF input tester.
- `tests/MFIGamepadFeeder.Tests`: automated tests. Simulated session tests need no controller; optional host-driver checks run when available.

Run the build and test commands above on Windows. Compiled output, personal device captures, and local machine settings are excluded from the source repository.

## License and credits

[MIT](LICENSE). The original project's copyright notice for Michał Mizera is retained. Thanks to [Axadiw/MFIGamepadFeeder](https://github.com/Axadiw/MFIGamepadFeeder), [HidSharp](https://www.zer7.com/software/hidsharp), and [Nefarius](https://github.com/nefarius) for the foundations and controller tooling.
