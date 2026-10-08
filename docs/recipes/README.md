# Recipes

Scenarios that take several tools. Each one was run on a real TIA Portal V21 project (06.10.2026) with temporary objects that were deleted afterwards. Tool parameters are in [`docs/tools/`](../tools/README.md). Every write is in memory until `save_project`.

- [A screen with a field and a button](screen-with-field-and-button.md)
- [Add a PLC and connect it to a subnet](add-plc-and-subnet.md)
- [Move a block to another PLC](move-block-between-plcs.md)
- [Download to a PLCSIM instance or a PLC](download-to-plcsim.md) (07.10.2026)
- [A SINAMICS drive and a PLC axis](drive-with-axis.md) (08.10.2026)
- [Protect a PLC](protect-a-plc.md) (08.10.2026)
- [Add a device from the catalog](add-device-from-catalog.md) (08.10.2026)

The last three were also replayed in one run through the group tools of the installed server (`tools/mcp-call.ps1 -Grouped -Wrap`, 55 calls, the refusals they quote came back as written).
