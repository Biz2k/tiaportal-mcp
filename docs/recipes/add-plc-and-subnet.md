# Add a PLC and connect it to a subnet

1. Find the catalog entry: `hw_search_catalog` with `query` (e.g. `CPU 1511-1 PN`) returns `typeIdentifier` values such as `OrderNumber:6ES7 511-1AK00-0AB0/V1.0`.
2. Create the station: `hw_create_device` with that `typeIdentifier`, a `name` for the PLC and a `stationName`. The answer lists the items made (`Rail_0`, the PLC).
3. Connect its PROFINET interface: `net_connect_subnet` with `deviceName` (the station), `interfaceName` (`PROFINET interface_1`) and `subnetName`. A subnet that does not exist is created as PN/IE.
4. Optional, for IO devices: `net_create_io_system` on the PLC interface, then `net_connect_subnet` and `net_connect_to_io_system` for each IO device (in this order; each step says which one it needs first).
5. Connections between two PLCs on the same subnet: `net_create_connection` (`s7`, `tcp`, `isoOnTcp`, `iso`, `udp`); `net_get_connections` lists them.
6. `save_project`.

To undo: `net_delete_subnet` refuses a subnet that still has interfaces or IO systems and names them; `force=true` deletes it with them. `hw_delete_device` removes the station.

Checked live with a temporary station and subnet; both were removed again.
