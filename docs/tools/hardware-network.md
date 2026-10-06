# Hardware and network

Devices, modules, subnets, IO systems and communication connections.

| Tool | Kind |
|---|---|
| [`hw_create_device`](#hw_create_device) | write |
| [`hw_delete_device`](#hw_delete_device) | write |
| [`hw_get_device_info`](#hw_get_device_info) | read |
| [`hw_get_device_item_info`](#hw_get_device_item_info) | read |
| [`hw_get_devices`](#hw_get_devices) | read |
| [`hw_get_topology`](#hw_get_topology) | read |
| [`hw_plug_module`](#hw_plug_module) | write |
| [`hw_search_catalog`](#hw_search_catalog) | read |
| [`net_connect_subnet`](#net_connect_subnet) | write |
| [`net_connect_to_io_system`](#net_connect_to_io_system) | write |
| [`net_create_connection`](#net_create_connection) | write |
| [`net_create_io_system`](#net_create_io_system) | write |
| [`net_delete_connection`](#net_delete_connection) | write |
| [`net_delete_subnet`](#net_delete_subnet) | write |
| [`net_disconnect_subnet`](#net_disconnect_subnet) | write |
| [`net_get_connections`](#net_get_connections) | read |

## hw_create_device

Create a hardware device (PLC, HMI, IO station) at the project level. With an 'OrderNumber:' or 'GSD:' identifier the station is created around that head module. With a 'System:Device.' identifier an empty station is created and the head module is added with 'hw_plug_module'. Use 'hw_search_catalog' to find identifiers

| Parameter | Type | Required | Description |
|---|---|---|---|
| `typeIdentifier` | string | yes | typeIdentifier: 'OrderNumber:<article>/<firmware>' (e.g. 'OrderNumber:6ES7 516-3AN02-0AB0/V2.9'), 'GSD:<file>/<type>', or 'System:Device.<type>' for an empty station (e.g. 'System:Device.ET200SP') |
| `name` | string | yes | name: name of the head module (the CPU or interface module); for a 'System:' identifier, the name of the station |
| `stationName` | string | no (default ``) | stationName: name of the station that holds the head module; empty (default) uses 'name' |

## hw_delete_device

Delete a hardware device (PLC, HMI, IO station) and everything in it from the project

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | yes | deviceName: path or name of the device, as 'hw_get_devices' returns it |

## hw_get_device_info

Get info from a device from the current project/session

| Parameter | Type | Required | Description |
|---|---|---|---|
| `devicePath` | string | yes | devicePath: path of the device as 'hw_get_devices' returns it, e.g. 'Group1/PC-System_1'. The device name alone, or the name of its CPU as the project tree shows it, is accepted when it is unique. A '/' inside a name is written '%2F' |

## hw_get_device_item_info

Get info from a device item from the current project/session

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceItemPath` | string | yes | deviceItemPath: device path followed by the item names, e.g. 'PC-System_1/Software PLC_1' or 'PLC_1/PROFINET interface_1'. The device name may be left out ('PLC_1'). A '/' inside a name is written '%2F' |

## hw_get_devices

Get a list of all devices in the project/session: path, name, type and the names of the top-level items. 'includeAttributes' adds every attribute of each device (long); 'hw_get_device_info' gives them for one device

| Parameter | Type | Required | Description |
|---|---|---|---|
| `includeAttributes` | boolean | no (default `False`) | includeAttributes: true adds the full attribute list of every device (default false) |
| `limit` | integer | no (default `500`) | limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so |
| `offset` | integer | no (default `0`) | offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0) |

## hw_get_topology

Get the hardware topology of the TIA-Portal project (devices, modules, MLFB/Article numbers, firmwares)

No parameters.

## hw_plug_module

Plug a new module into a rack or module of an existing device at a position. 'hw_get_topology' shows the racks and what is already plugged

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | yes | deviceName: path or name of the device, as 'hw_get_devices' returns it |
| `parentItemName` | string | yes | parentItemName: name of the item to plug into, usually the rack (e.g. 'Rack_0' or 'Rail_0'). Empty plugs into the station itself, which is how a rack is added to an empty station (typeIdentifier e.g. 'System:Rack.ET200SP', position 0) |
| `positionNumber` | integer | yes | positionNumber: the slot to plug into (e.g. 1) |
| `typeIdentifier` | string | yes | typeIdentifier: Openness type identifier of the module (e.g. 'OrderNumber:6ES7 131-6BH01-0BA0/V0.0'); 'hw_search_catalog' finds it |
| `moduleName` | string | yes | moduleName: the name for the new module |

## hw_search_catalog

Search the installed hardware catalog by article number or product name and get the type identifiers that 'hw_create_device' and 'hw_plug_module' need. Needs a connection to TIA Portal, not an open project

| Parameter | Type | Required | Description |
|---|---|---|---|
| `query` | string | yes | query: at least three characters of an article number or product name, e.g. '6ES7 155-6AU01' or 'IM 155-6 PN' |
| `maxResults` | integer | no (default `20`) | maxResults: stop after this many entries (default 20) |

## net_connect_subnet

Connect a network interface to a subnet; a PN/IE subnet of that name is created if it does not exist. Steps 1 and 3 of building a PROFINET IO system: connect the PLC interface, create the IO system ('net_create_io_system'), connect the IO device interface to the same subnet, then 'net_connect_to_io_system'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | yes | deviceName: path or name of the device, as 'hw_get_devices' returns it |
| `interfaceName` | string | yes | interfaceName: name of the PROFINET/Ethernet interface item (e.g. 'PROFINET interface_1') |
| `subnetName` | string | yes | subnetName: name of the subnet to connect to (e.g. 'PN/IE_1') |

## net_connect_to_io_system

Connect the network interface of an IO device to an existing IO system. Step 4 of building an IO system: the IO device interface must already be on the IO system's subnet ('net_connect_subnet')

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | yes | deviceName: path or name of the IO device, as 'hw_get_devices' returns it |
| `interfaceName` | string | yes | interfaceName: name of the PROFINET interface item of the IO device |
| `ioSystemName` | string | yes | ioSystemName: name of the IO system to join |

## net_create_connection

Create a connection between two PLCs of the project. Both need an interface on a common subnet ('net_connect_subnet'). Connections to an HMI are made with 'unified_manage_connections'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `localPlc` | string | yes | localPlc: path of the PLC that owns the connection, as the plc_* tools take it (e.g. 'PLC_1' or 'Station_1/PLC_1') |
| `partnerPlc` | string | yes | partnerPlc: path of the partner PLC |
| `connectionType` | string | no (default `s7`) | connectionType: 's7' (default), 'tcp', 'isoOnTcp', 'iso' or 'udp' |
| `localInterface` | string | no (default ``) | localInterface: interface item name of the local PLC; empty picks one that shares a subnet with the partner |
| `partnerInterface` | string | no (default ``) | partnerInterface: interface item name of the partner PLC; empty picks one that shares a subnet |
| `name` | string | no (default ``) | name: connection name; empty keeps the name TIA Portal gives |

## net_create_io_system

Create a PROFINET IO system on a PLC network interface. Step 2 of building an IO system: the interface must already be connected to a subnet with 'net_connect_subnet'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | yes | deviceName: path or name of the PLC device, as 'hw_get_devices' returns it |
| `interfaceName` | string | yes | interfaceName: name of the PROFINET interface item of the PLC (e.g. 'PROFINET interface_1') |
| `ioSystemName` | string | yes | ioSystemName: name of the IO system to create (e.g. 'PROFINET IO-System (100)') |

## net_delete_connection

Delete a communication connection of a PLC by its name ('net_get_connections' lists them). The partner's end goes with it

| Parameter | Type | Required | Description |
|---|---|---|---|
| `localPlc` | string | yes | localPlc: path of the PLC that owns the connection |
| `connectionName` | string | yes | connectionName: name of the connection |

## net_delete_subnet

Delete a subnet of the project. A subnet that still has connected interfaces or IO systems is refused with the list of them; 'force' deletes it together with them. Subnets are listed by 'hw_get_topology'

| Parameter | Type | Required | Description |
|---|---|---|---|
| `subnetName` | string | yes | subnetName: name of the subnet (e.g. 'PN/IE_1') |
| `force` | boolean | no (default `False`) | force: delete the subnet although interfaces or IO systems are on it (default false) |

## net_disconnect_subnet

Disconnect a network interface from its subnet. An IO device leaves its IO system with it

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | yes | deviceName: path or name of the device, as 'hw_get_devices' returns it |
| `interfaceName` | string | yes | interfaceName: name of the PROFINET/Ethernet interface item |

## net_get_connections

List the communication connections (S7, TCP, ISO-on-TCP, ISO, UDP, HMI) of the project or of one device: owner, name, type, partner, subnet, addresses and the type-specific settings. A connection is listed by both of its ends

| Parameter | Type | Required | Description |
|---|---|---|---|
| `deviceName` | string | no (default ``) | deviceName: path or name of one device, as 'hw_get_devices' returns it; empty lists the whole project |

