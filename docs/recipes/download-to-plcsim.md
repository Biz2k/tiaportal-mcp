# Download to a PLCSIM instance or a PLC

The user starts PLCSIM (or the PLC) and creates the instance; the server never starts it. A download changes a running controller: do it only when the user asked for this download.

1. Make sure the device compiles: `hw_compile` with the name of the station. It compiles hardware and software and returns the errors with the object each belongs to. `download_to_plc` only says that the hardware does not compile.
2. `get_download_targets` with the `softwarePath` of the PLC lists the targets as mode / PC interface / target interface, e.g. `PN/IE` / `Siemens PLCSIM Virtual Ethernet Adapter` / `1 X1`. Take the one the user confirmed.
3. `get_accessible_devices` with that PC interface shows what answers behind it, `pcAddresses` (the addresses of the PC interface) and `downloadAddresses` (the addresses the project gives the PLC).
   - The list is the one of TIA Portal and is refreshed only by its window "Online access > Update accessible devices". A device started or readdressed a moment ago can be missing or shown at its old address: ask the user to refresh there.
   - A device outside the subnets of `pcAddresses` is found but cannot be loaded. See "Another subnet" below.
4. `download_to_plc` with `hardware: true`, `software: true`, `stopPlc: true` (a hardware download needs the stop - tell the user before the call) and:
   - `trustDevice: true` when the device shows a certificate TIA Portal cannot verify - every PLCSIM instance on the first connection. Only after the user confirmed that this is their device; without it the call is refused and says what TIA Portal found.
   - `targetAddress` when the device answers at another address than the project gives the PLC (an empty instance at `192.168.0.1`). An address nothing answers at is refused before the download starts.
   - `passwords` when the PLC is protected: `{"ModuleWriteAccessPassword": "..."}` for the access level. Only a password the user gave. A step that asked and got none is named under `steps`.
   - `downloadUserManagement` (`keep`, `update`, `overwrite`) when the CPU holds users that differ from the project.
5. Read `steps` and the messages of the answer. Two passes are normal:
   - with `targetAddress` the device takes the address of the project when the hardware is loaded, and the rest of the call fails - repeat the call without `targetAddress`;
   - an empty instance often takes the hardware in the first pass and the program in the second - repeat with the same arguments (the certificate is not asked again).
6. `startPlc: true` in the last pass starts the CPU.

## Another subnet

The PC interface must have an address in the subnet of the device. The dialog of TIA Portal adds a temporary one; Openness does not, and the server does not change the network settings of the PC. Say what is missing and offer the user two ways:

- the user adds an address of that subnet to the PC interface (Windows network settings, or `netsh interface ipv4 add address "<adapter>" <address> <mask>` as administrator);
- after the user's yes, you move the PLC: set a port of the PLC - an unconfigured one (`0.0.0.0`), else the first - to an address in a subnet of the PC interface with `hw_set_device_item_attributes` (`Node.Address` on the interface), keep the old value, compile, load, and put the old address back when the simulation is over.

A port with connections to partners in its subnet (IO devices, S7 connections) cannot be moved: the hardware stops compiling. Put the address back and say so.

## What the refusals mean

| Answer | Meaning |
|---|---|
| the device might not be trustworthy | certificate not verified: ask the user, then `trustDevice: true` |
| nothing answers at the address | wrong address, instance not running, or the PC has no address in that subnet |
| the CPU does not match | the instance is of another CPU family than the PLC of the project (a 1500 instance for an ET 200SP CPU); the user creates a matching instance |
| the hardware does not compile | `hw_compile` names the errors |

Checked live on PLCSIM (07.10.2026): a CPU 1500 and an ET 200SP CPU into empty instances in two passes each, a PLC with access-level passwords, a PLC moved to another subnet and back. Not checked: the step `PlcMasterSecretPassword`, a CPU with firmware V4 that asks for a user at the connection.
