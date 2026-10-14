#!/usr/bin/env python3
"""GAL-012 deterministic authoring from canonical MIG-014 stubs; no Unity execution.
Run from the project root, then use GAL-012-import-world.cs in the lead's Editor.
Existing identities/descriptions/GUIDs and approved art are preserved.
"""
from pathlib import Path
import re, uuid

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / 'Assets/Game/Content'
NAMESPACE = uuid.UUID('d759aec0-f931-48ca-a5eb-055b7f2178c1')

def guid(path):
    meta = Path(str(ROOT / path) + '.meta')
    if not meta.exists():
        raise RuntimeError(f'Required real asset metadata missing: {path}')
    return re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)[1]

def identity(path):
    return uuid.uuid5(NAMESPACE, path).hex

def ref(path, file_id=11400000):
    return '{fileID: %d, guid: %s, type: 2}' % (file_id, guid(path))

def meta(path, folder=False):
    p = ROOT / path
    m = Path(str(p) + '.meta')
    if m.exists(): return
    text = 'fileFormatVersion: 2\nguid: ' + identity(path) + '\n'
    text += ('folderAsset: yes\nDefaultImporter:\n' if folder else 'NativeFormatImporter:\n')
    text += '  externalObjects: {}\n'
    if not folder: text += '  mainObjectFileID: 11400000\n'
    text += '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    m.write_text(text)

def asset(path, type_name, fields):
    p = ROOT / path; p.parent.mkdir(parents=True, exist_ok=True)
    meta(str(p.parent.relative_to(ROOT)), True)
    key = p.stem
    header = '''%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier: Game.Data::Game.Data.Definitions.%s
  id: %s
  formerIds: []
''' % (guid('Assets/Game/Data/Definitions/World/' + type_name + '.cs'), key, type_name, key)
    p.write_text('\n'.join(line.rstrip() for line in (header + fields).splitlines()) + '\n'); meta(path)

def body_ref(key): return ref('Assets/Game/Content/Bodies/' + key + '.asset')
def galaxy_ref(key): return ref('Assets/Game/Content/Galaxy/' + key + '.asset')

# Complete imported stubs in place; update only GAL-012-owned serialized values.
# Preserve all MIG-014 identity fields, migration descriptions, metadata and approved art bytes.
TAGS = dict(zip('station station_ark station_abandoned derelict_ship debris_field resource_cache gate_dormant gate_active jump_point asteroid_belt planet_barren planet_gas_giant planet_ice planet_ice_giant planet_rocky planet_terrestrial planet_water moon star star_blackhole'.split(),
                'station ark_station abandoned_station derelict debris_field resource_cache gate_dormant gate_active jump_point asteroid_belt barren_planet gas_giant ice_planet ice_giant rocky_planet terrestrial_planet ocean_planet moon star black_hole'.split()))
for suffix, tag in TAGS.items():
    p = CONTENT / 'Bodies' / ('body_' + suffix + '.asset')
    if not p.exists() or not re.search(r'^  id: body_' + suffix + r'$', p.read_text(), re.M):
        raise RuntimeError('Missing canonical MIG-014 stub: ' + str(p))
    if not re.search(r'^  storyTag: ' + tag + r'$', p.read_text(), re.M):
        raise RuntimeError('Canonical narrative tag differs: ' + str(p))
    guid(str(p.relative_to(ROOT)))
for path in ['Assets/Game/Content/Environment/Nocturne/VesperSphere.asset',
             'Assets/Game/Content/Environment/Nocturne/M_VesperSurface.mat',
             'Assets/Game/Content/Blocks/block_hull.asset', 'Assets/Game/Content/Blocks/block_core_small.asset',
             'Assets/Game/Content/Ships/ship_vestari.asset', 'Assets/Game/Content/Ships/ship_frigate.asset']:
    if not (ROOT / path).exists(): raise RuntimeError('Missing real resource: ' + path)
    guid(path)

for key in ['bodyvis_nocturne_celestial', 'bodyvis_nocturne_structure']:
    asset('Assets/Game/Content/Bodies/' + key + '.asset', 'BodyVisualDefinition',
          '  mesh: ' + ref('Assets/Game/Content/Environment/Nocturne/VesperSphere.asset', 4300000) + '\n'
          '  meshRadius: 1\n  material: ' + ref('Assets/Game/Content/Environment/Nocturne/M_VesperSurface.mat', 2100000) + '\n'
          '  baseColor: {r: 1, g: 1, b: 1, a: 1}\n  emissionColor: {r: 0, g: 0, b: 0, a: 1}\n'
          '  castShadows: 0\n  hideWhenGridPresent: 1\n  mapIcon: {fileID: 0}\n  mapColorToken: --color-map-body\n')

hull = ref('Assets/Game/Content/Blocks/block_hull.asset')
core = ref('Assets/Game/Content/Blocks/block_core_small.asset')
for suffix in TAGS:
    p = CONTENT / 'Bodies' / ('body_' + suffix + '.asset'); text = p.read_text()
    celestial = suffix.startswith('planet_') or suffix in ['star', 'star_blackhole', 'moon']
    belt = suffix == 'asteroid_belt'; gate = suffix in ['gate_active', 'gate_dormant', 'jump_point']
    station = suffix.startswith('station'); blueprint = suffix in ['station', 'derelict_ship']
    # TRV-E007 uses the full sizeCells diagonal at the real 0.5 m default: 29.12 m for gates.
    radius = 1000 if suffix.startswith('star') or belt else 100 if suffix == 'moon' else 650 if suffix.startswith('planet_') else 40 if station else 32 if gate else 20 if suffix == 'derelict_ship' else 6
    values = {'radiusMinM': radius, 'radiusMaxM': radius,
              'visual': body_ref('bodyvis_nocturne_celestial' if celestial else 'bodyvis_nocturne_structure') if not belt else '{fileID: 0}',
              'starClass': 9 if suffix == 'star_blackhole' else 2,
              'hasCollider': int(celestial or not belt), 'canHaveMoons': int(suffix.startswith('planet_')),
              'interaction': 0 if celestial or belt else 3 if gate else 1 if station else 5 if suffix in ['derelict_ship','debris_field'] else 2,
              'interactPrompt': 'Dock' if station else 'Jump' if gate else 'Salvage' if suffix in ['derelict_ship','debris_field'] else 'Inspect' if not celestial and not belt else '',
              'structureSource': 0 if celestial or belt else 1 if blueprint else 2,
              'blueprint': ref('Assets/Game/Content/Ships/' + ('ship_vestari' if suffix == 'station' else 'ship_frigate') + '.asset') if blueprint else '{fileID: 0}',
              'destructible': int(not celestial and not gate), 'asteroidBlock': hull if belt else '{fileID: 0}'}
    # SPEC-30 Q36 / SYS-003: the two packed sector rings must contain the 2000 m belt.
    # Author this explicitly so fresh MIG-014 stubs cannot retain their old 500 m default.
    if belt:
        values.update(beltWidthM=2000, sectorSizeM=1000)
    for key, value in values.items():
        text, count = re.subn(r'^  ' + key + r':.*$', '  ' + key + ': ' + str(value), text, flags=re.M)
        if count != 1: raise RuntimeError(f'Missing/duplicate field {p}:{key}')
    shape = ('  shape:\n    kind: %d\n    sizeCells: {x: %d, y: %d, z: %d}\n    wallThicknessCells: 2\n'
             '    hullBlock: %s\n    coreBlock: %s\n    colorIndex: 0\n') % (
                0 if station or gate else 2, 64 if station else 48 if gate else 12,
                12 if station else 8, 40 if station else 32 if gate else 12,
                hull, core if not gate and not celestial and not belt and not blueprint else '{fileID: 0}')
    if values['structureSource'] == 2:
        text, count = re.subn(r'^  shape:\n(?:    .*\n)+', shape, text, flags=re.M)
        if count != 1: raise RuntimeError('Shape field not found: ' + str(p))
    p.write_text(text)

fields = '  namePrefix: Uncharted\n'
for pool, key in [('starPool','body_star'),('planetPool','body_planet_rocky'),('moonPool','body_moon'),('beltPool','body_asteroid_belt'),('stationPool','body_station'),('pointOfInterestPool','body_resource_cache')]:
    fields += '  ' + pool + ':\n  - template: ' + body_ref(key) + '\n    weight: 1\n    maxCount: 0\n'
for key, value in dict(minPlanets=0,maxPlanets=2,maxMoonsPerPlanet=1,moonChance=.5,minBelts=0,maxBelts=1,minStations=0,maxStations=1,minPointsOfInterest=0,maxPointsOfInterest=2,
                       firstOrbitRadiusM=7000,orbitSpacingMinM=4000,orbitSpacingMaxM=5000,orbitPeriodAtReferenceS=3600,orbitReferenceRadiusM=4000,
                       moonOrbitMinM=1200,moonOrbitMaxM=1800,moonPeriodAtReferenceS=600,moonReferenceRadiusM=1000,
                       stationRadiusMinM=3000,stationRadiusMaxM=9000,pointOfInterestRadiusMinM=6000,pointOfInterestRadiusMaxM=12000,
                       minBodySeparationM=1500,laneRingMarginM=3000,minLaneRingRadiusM=22000,extentMarginM=2000,dangerMin=0,dangerMax=3).items(): fields += f'  {key}: {value}\n'
asset('Assets/Game/Content/Galaxy/systpl_opening.asset','StarSystemTemplateDefinition',fields)

def body(key, suffix, role, slot, radius, phase, parent=''):
    return f'''  - key: {key}
    role: {role}
    slot: {slot}
    parentKey: {parent}
    template: {body_ref('body_' + suffix)}
    mode: {1 if role in [2,3] else 0}
    orbitRadiusM: {radius}
    phaseDegrees: {phase}
    eccentricity: 0
    periodOverrideS: 0
    radiusOverrideM: 0
    yawDegrees: 0
    displayNameOverride:
'''

SYSTEMS = [('starsys_ark_start','Ark Reach',0,0,0,True,
            [('ark_station','station_ark',4,0,3000,0,''),('reach_i','planet_rocky',2,0,7000,45,''),('reach_ii','planet_terrestrial',2,1,12000,200,''),('reach_ii_a','moon',3,0,1400,0,'reach_ii'),('reach_belt','asteroid_belt',5,0,18000,0,'')]),
           ('starsys_drift','The Drift',6,2,2,False,
            [('drift_belt','asteroid_belt',5,0,10000,0,''),('drift_derelict','derelict_ship',8,0,8000,180,''),('drift_cache','resource_cache',8,1,10500,30,''),('drift_lab','station_abandoned',4,0,5000,90,'')]),
           ('starsys_whisper','Whisper',-5,4,1,False,
            [('whisper_i','planet_ice_giant',2,0,7000,40,''),('whisper_ii','planet_barren',2,1,12000,210,''),('whisper_outpost','station',4,0,4000,0,'')])]
for key,name,x,y,danger,hub,bodies in SYSTEMS:
    fields = f'  displayName: {name}\n  positionLy: {{x: {x}, y: {y}}}\n  template: {galaxy_ref("systpl_opening")}\n  star: {body_ref("body_star")}\n  bodies:\n'
    fields += ''.join(body(*row) for row in bodies)
    fields += f'  hiddenUntilKnown: 0\n  isStory: 1\n  isHub: {int(hub)}\n  noEncounters: 0\n  connectProcedurally: 0\n  danger: {danger}\n  startBodyKey: {"ark_station" if hub else ""}\n  startOffsetM: {{x: 0, y: 0, z: -400}}\n  startYawDegrees: 0\n'
    asset('Assets/Game/Content/Galaxy/'+key+'.asset','AuthoredStarSystemDefinition',fields)
fields = '''  generatorVersion: 1
  useFixedSeed: 1
  fixedSeed: 1592590337
  galaxyRadiusLy: 60
  proceduralStarSystemCount: 0
  minStarSystemSpacingLy: 3
  placementAttemptsPerStarSystem: 30
  laneCandidateNeighbours: 6
  maxLaneDegree: 4
  maxLaneLengthLy: 12
  extraLaneChance: 0.35
  templatePool:
  - template: %s
    weight: 1
  authoredStarSystems:
''' % galaxy_ref('systpl_opening')
fields += ''.join('  - '+galaxy_ref(row[0])+'\n' for row in SYSTEMS)
fields += '  startStarSystem: '+galaxy_ref('starsys_ark_start')+'\n  authoredLanes:\n'
for key, gate in [('starsys_drift',False),('starsys_whisper',True)]:
    fields += '  - a: '+galaxy_ref('starsys_ark_start')+'\n    b: '+galaxy_ref(key)+'\n    kind: 0\n    condition:\n'
    fields += '      source: '+('$gate_ark_start_active' if gate else '')+'\n      sourceHash: 0\n      compilerVersion: 0\n      hasErrors: '+str(int(gate))+'\n      root: {rid: -2}\n'
    fields += '    lockedMessage: '+('The gate is dormant.' if gate else '')+'\n'
    for end in ['A','B']: fields += '    gateTemplate'+end+': '+(body_ref('body_gate_dormant') if gate else '{fileID: 0}')+'\n'
    fields += '    hiddenUntilKnown: 0\n'
fields += '  defaultHyperlaneGate: '+body_ref('body_jump_point')+'\n  defaultAncientGate: '+body_ref('body_gate_active')+'\n  references:\n    version: 2\n    RefIds:\n    - rid: -2\n      type: {class: , ns: , asm: }\n'
asset('Assets/Game/Content/Galaxy/galaxy_main.asset','GalaxyGenerationDefinition',fields)
print('Authored 3 real star systems, 2 lanes, galaxy_main, 1 template and 2 approved-art visual definitions; completed 20 canonical body stubs.')
print('Raw gate source is serialized fail-closed; the lead import recipe must compile it through the actual TRV-003/NAR-009 owner before gameplay.')
