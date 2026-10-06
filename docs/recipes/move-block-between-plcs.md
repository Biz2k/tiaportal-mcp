# Move a block to another PLC

`plc_move_block` moves a block between groups of one PLC. To another PLC a block is copied, then deleted at the source.

1. Make sure the block is consistent: `plc_compile_block` with `softwarePath` and `blockPath`. A block that was never compiled cannot be copied.
2. Copy it: `plc_copy_block` with `softwarePath` (source PLC), `blockPath`, `targetSoftwarePath` (the other PLC) and `targetGroupPath` (empty for the root). The copy keeps the name when it goes to another PLC; inside one PLC `newName` is required.
3. Compile the copy in the target PLC (`plc_compile_block`): it arrives not yet compiled, `isConsistent` is false until then.
4. Check the target: `plc_get_blocks` on the target PLC. `plc_where_used` on the source tells who calls the block; check those callers before the original goes (not checked live whether calls follow a copy).
5. Delete the original with `plc_delete_block` once nothing uses it, then `save_project`.

Checked live with a temporary SCL function made by `plc_create_scl_block` in one PLC and copied to a second one; both copies were deleted again.
