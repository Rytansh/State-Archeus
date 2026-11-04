#!/usr/bin/env python3
"""Read-only import metadata and test-assembly checks; does not replace Unity compilation."""
from pathlib import Path
import json
import re
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
guids = {}
for path in (root / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
    if not match:
        raise SystemExit(f'Invalid Unity GUID metadata: {path}')
    if match[1] in guids:
        raise SystemExit(f'Duplicate Unity GUID: {path} and {guids[match[1]]}')
    guids[match[1]] = path

tracked = subprocess.check_output(['git', 'ls-files', '-z', 'Assets'], cwd=root).decode().split('\0')
untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '-z', 'Assets'], cwd=root).decode().split('\0')
for relative in tracked + untracked:
    if not relative or relative.endswith('.meta'):
        continue
    path = root / relative
    if not path.exists():
        continue
    if not Path(str(path) + '.meta').exists():
        raise SystemExit(f'Missing Unity metadata: {relative}')

edit = json.loads((root / 'Assets/Testing/EditMode/EditMode.asmdef').read_text())
play = json.loads((root / 'Assets/Testing/PlayMode/PlayMode.asmdef').read_text())
assert {'GameAssembly', 'Unity.Entities', 'Unity.Collections', 'UnityEngine.TestRunner', 'UnityEditor.TestRunner'} <= set(edit['references'])
assert edit['includePlatforms'] == ['Editor']
assert {'GameAssembly', 'Unity.Entities', 'Unity.Collections', 'Unity.Timeline', 'UnityEngine.TestRunner'} <= set(play['references'])
for definition in (edit, play):
    assert definition['overrideReferences']
    assert 'nunit.framework.dll' in definition['precompiledReferences']
    assert 'UNITY_INCLUDE_TESTS' in definition['defineConstraints']
    assert not definition['autoReferenced']
ET.parse(root / 'Assets/Battle/Simulation/Snapshots/link.xml')
print(f'Unity metadata: {len(guids)} unique GUIDs; Edit Mode/Play Mode assembly conventions and linker XML valid')
