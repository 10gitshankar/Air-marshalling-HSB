# KiCad — Schematic & PCB
- `AirMarshalling.kicad_pro`
- `AirMarshalling.kicad_sch`
- `AirMarshalling.kicad_pcb`

Put plotted/exported outputs (Gerbers, schematic PDF) in `Exports/`.

## Workflow notes

- Always open the project via the `.kicad_pro` file, not the `.kicad_sch`
  directly — otherwise `Tools → Update PCB from Schematic (F8)` won't work
  correctly.
- Export via `File → Plot`.
- Watch for nested unit symbol names retaining a stray `local:` library
  prefix — check symbol names after any library re-import.

## Pending decisions (not yet finalized in the schematic)

- Power LED: green, 3V3 → 330Ω → GND, hardware-only (no GPIO).
- Low-battery LED: red, driven from GPIO25, with a voltage divider on
  GPIO34 using two 100kΩ resistors.
- Both depend on confirming whether the final build uses a direct LiPo cell
  or a USB power bank — finalize before routing these nets.
