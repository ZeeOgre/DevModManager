# ModGroups Support for Load Order Rule Derivation

## Overview

ModGroups are a mechanism originally from xEdit that allows mod authors and users to define load order relationships between plugins. This document describes how applications should implement support for reading and processing modgroup files to derive load order rules.

## File Format Specification

### File Discovery

1. **Plugin-specific files**: For each loaded plugin `PluginName.ext`, check for `PluginName.modgroups` in the same directory
2. **Global file**: Check for `modgroups.txt` in the game's data directory
3. **Loading condition**: `.modgroups` files are only loaded if a corresponding plugin with the same base name exists in the load order

### File Structure

ModGroup files use standard INI format:

```ini
[ModGroupName]
item1
item2
...

[AnotherModGroup]
item1
item2
...
```

### Item Syntax

Each item follows this pattern:
```
[flags]filename[:crc32,crc32,...]
```

#### Flags (optional prefix characters)

| Flag | Meaning | Load Order Impact |
|------|---------|-------------------|
| `+` | Optional | File not required for group to be active |
| `!` | Forbidden | Group invalid if file exists with matching CRC |
| `{` | Block member | Can load in any order relative to other block members |
| `}` | Ignore order | No load order requirements |
| `-` | Neither source nor target | (Ignored for load order derivation) |
| `@` | Target only | (Ignored for load order derivation) |
| `#` | Source only | (Ignored for load order derivation) |

Default (no flags): Required file that follows normal load order rules

#### CRC32 List (optional)

- Format: `:crc32[,crc32,...]`
- Each CRC32 is 8 hexadecimal digits (case-insensitive)
- If specified, the file must have one of the listed CRC32 values to be considered "present"
- If omitted, any CRC32 is accepted

### Examples

```ini
[UI Overhaul Complete]
UICore.esm
UIExtensions.esp
+UIOptionalAddon.esp:1234ABCD,5678EF01
!OldUIVersion.esp:DEADBEEF

[Quest Package]
QuestBase.esm
{QuestAddon1.esp
{QuestAddon2.esp
{QuestAddon3.esp
QuestFinale.esp
}CompatibilityPatch.esp
```

## Processing Rules

### Step 1: File Discovery and Parsing

```
FOR each plugin P in load order:
    IF file exists: P.modgroups
        Load and parse all modgroups from file
        
IF file exists: modgroups.txt
    Load and parse all modgroups from file
```

### Step 2: Determine Active ModGroups

A modgroup is **active** if and only if:

```
FUNCTION IsModGroupActive(modgroup):
    FOR each item in modgroup:
        exists = FileExists(item.filename) AND 
                 (item.crcList.empty OR file.CRC32 in item.crcList)
        
        IF item.isForbidden AND exists:
            RETURN false  // Forbidden file present
            
        IF item.isRequired AND NOT exists:
            RETURN false  // Required file missing or wrong CRC
            
    RETURN true
```

### Step 3: Generate Load Order Rules

For each active modgroup, generate load-after rules:

```
FUNCTION GetLoadAfterRules(modgroup):
    rules = []
    processedFiles = []
    blockFiles = []
    
    FOR each item in modgroup:
        IF NOT FileExists(item):  // Using same "exists" logic as above
            CONTINUE
            
        IF item.ignoreLoadOrderAlways:
            CONTINUE  // No rules for this file
            
        IF item.ignoreLoadOrderInBlock:
            // Block items still load after everything before the block
            FOR each file in processedFiles:
                rules.add(item.filename loads after file)
            blockFiles.add(item.filename)
        ELSE:
            // End of block - all block files become "processed"
            processedFiles.addAll(blockFiles)
            blockFiles.clear()
            
            // Current file must load after all processed files
            FOR each file in processedFiles:
                rules.add(item.filename loads after file)
                
            processedFiles.add(item.filename)
    
    // Handle any remaining block files
    processedFiles.addAll(blockFiles)
    
    RETURN rules
```

## Testing Scenarios

### Scenario 1: Simple Linear Order
```ini
[TestGroup]
First.esm
Second.esp
Third.esp
```
Expected: Third loads after Second loads after First

### Scenario 2: With Optional Files
```ini
[TestGroup]
Required.esm
+Optional.esp
AlsoRequired.esp
```
- If Optional.esp missing: Group active, AlsoRequired loads after Required
- If Optional.esp present: AlsoRequired loads after Optional loads after Required

### Scenario 3: Block Handling
```ini
[TestGroup]
Base.esm
{Addon1.esp
{Addon2.esp
{Addon3.esp
Final.esp
```
Expected: 
- Addon1, Addon2, Addon3 all load after Base
- Addon1, Addon2, Addon3 can be in any order relative to each other
- Final loads after Base AND all three Addons

### Scenario 4: CRC Validation
```ini
[TestGroup]
Mod.esp:12345678
Patch.esp
```
- If Mod.esp has CRC 12345678: Group active
- If Mod.esp has different CRC: Group inactive
- Patch.esp accepts any CRC

## Notes for Implementation

**Parser Requirements**:
 - Handle Windows and Unix line endings
 - Skip empty lines and lines starting with `;` (comments)
 - Trim whitespace from section names and items
