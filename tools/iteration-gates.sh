#!/bin/zsh
# SPEC-71 TST-008: three real runs per developer-iteration gate; needs this project open in an idle Editor.
. "$(dirname "$0")/_env.sh"
if [[ "$GAME_UNITY_TOOL_LOCK_ROOT" != "${PROJECT_ROOT:A}" ]]; then
  exec python3 "$TOOLS_DIR/unity_tool_lock.py" "$PROJECT_ROOT" -- "$TOOLS_DIR/iteration-gates.sh" "$@"
fi
python3 - "$PROJECT_ROOT" <<'PY'
import json, pathlib, shutil, statistics, subprocess, sys, tempfile, time
root = pathlib.Path(sys.argv[1])
logs = root / 'Logs' / 'iteration'
logs.mkdir(parents=True, exist_ok=True)

def command(name, *args):
    run = subprocess.run(['unity', 'command', name, *args, '--project-path', str(root), '--timeout', '60', '--format', 'json'], capture_output=True, text=True, timeout=70)
    if run.returncode: raise RuntimeError(f'{name}: {run.stdout[-1000:]} {run.stderr[-300:]}')
    response = json.loads(run.stdout)
    if not response.get('success'): raise RuntimeError(f'{name}: {response.get("errors")}')
    result = response['data']['result']
    if isinstance(result, dict) and result.get('success') is False: raise RuntimeError(f'{name}: {result}')
    return result

def evaluate(code):
    result = command('eval', '--code', code)
    return result['result']

def wait_stopped():
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        state = command('editor_status')
        if state['playMode'] == 'stopped' and state['status'] == 'ready': return
        time.sleep(.05)
    raise RuntimeError('Editor did not return to idle Edit Mode.')

rows = []
def measure(name, budget, action):
    samples, errors = [], []
    for i in range(3):
        try:
            start = time.monotonic()
            action(i)
            samples.append(time.monotonic() - start)
        except Exception as e:
            errors.append(str(e))
    median = statistics.median(samples) if len(samples) == 3 else None
    rows.append(dict(gate=name, samplesSeconds=samples, medianSeconds=median, budgetSeconds=budget,
                     passed=not errors and median is not None and median <= budget, errors=errors))

def run_script(script, label, i):
    with (logs / f'{label}-{i+1}.log').open('w') as stream:
        result = subprocess.run([str(root / script)], cwd=root, stdout=stream, stderr=subprocess.STDOUT, timeout=1800)
    if result.returncode: raise RuntimeError(f'{script} failed: Logs/iteration/{label}-{i+1}.log')

ready_code = ('return UnityEngine.Application.isPlaying && '
              'Game.Core.Services.TryGet<Game.App.Flow.SceneFlow>(out var flow) && '
              '!flow.IsTransitioning && flow.CurrentScene == Game.Core.Flow.GameScene.MainMenu;')
setup = None
hooks = None
profile = None
try:
    state = command('editor_status')
    if state['playMode'] != 'stopped' or state['status'] != 'ready': raise RuntimeError('Requires an idle Editor in Edit Mode.')
    # Never discard an unsaved scene, and restore the original scene setup after the measurements.
    setup = evaluate('var scenes = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup(); '
                     'for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) '
                     'if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty || (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path.Length == 0 && UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).rootCount != 0)) throw new Exception("Save dirty or populated unnamed scenes before measuring."); return scenes;')
    measure('Compile (no changes)', 10, lambda i: run_script('tools/compile.sh', 'compile', i))
    command('open_scene', '--path', 'Assets/Game/Scenes/Boot.unity')
    hooks = evaluate('return new { SaveRoot = Game.App.Testing.GameRootTestHooks.SaveRoot, SettingsPath = Game.App.Testing.GameRootTestHooks.SettingsPath, SkipIntroScreens = Game.App.Testing.GameRootTestHooks.SkipIntroScreens };')
    profile = tempfile.mkdtemp(prefix='profile-', dir=logs)
    evaluate('Game.App.Testing.GameRootTestHooks.SaveRoot = ' + json.dumps(profile + '/saves') + '; Game.App.Testing.GameRootTestHooks.SettingsPath = ' + json.dumps(profile + '/settings.json') + '; Game.App.Testing.GameRootTestHooks.SkipIntroScreens = true;')
    evaluate(ready_code)  # Warm only the CLI eval compiler, before entering Play Mode.
    def play(i):
        command('editor_play')
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            if evaluate(ready_code): return
            time.sleep(.01)
        raise RuntimeError('Boot did not reach MainMenu in 30 s.')
    for i in range(3):
        samples = rows[-1] if rows and rows[-1]['gate'] == 'Enter Play Mode' else None
        if samples is None:
            samples = dict(gate='Enter Play Mode', samplesSeconds=[], medianSeconds=None, budgetSeconds=1, passed=False, errors=[]); rows.append(samples)
        try:
            start = time.monotonic(); play(i); samples['samplesSeconds'].append(time.monotonic() - start)
        except Exception as e: samples['errors'].append(str(e))
        finally:
            command('editor_stop'); wait_stopped()
    samples['medianSeconds'] = statistics.median(samples['samplesSeconds']) if len(samples['samplesSeconds']) == 3 else None
    samples['passed'] = not samples['errors'] and samples['medianSeconds'] is not None and samples['medianSeconds'] <= 1
    measure('EditMode suite', 60, lambda i: run_script('tools/test.sh', 'editmode', i))
except Exception as e:
    for name, budget in [('Compile (no changes)', 10), ('Enter Play Mode', 1), ('EditMode suite', 60)]:
        if not any(r['gate'] == name for r in rows):
            rows.append(dict(gate=name, samplesSeconds=[], medianSeconds=None, budgetSeconds=budget, passed=False, errors=[str(e)]))
finally:
    if setup is not None:
        try:
            state = command('editor_status')
            if state['playMode'] != 'stopped': command('editor_stop'); wait_stopped()
            if hooks is not None:
                evaluate('Game.App.Testing.GameRootTestHooks.SaveRoot = ' + json.dumps(hooks['SaveRoot']) + '; Game.App.Testing.GameRootTestHooks.SettingsPath = ' + json.dumps(hooks['SettingsPath']) + '; Game.App.Testing.GameRootTestHooks.SkipIntroScreens = ' + str(hooks['SkipIntroScreens']).lower() + ';')
            if profile is not None: shutil.rmtree(profile)
            if not setup or all(not scene['path'] for scene in setup):
                evaluate('UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);')
            else:
                scenes = ','.join('new UnityEditor.SceneManagement.SceneSetup { path = ' + json.dumps(scene['path']) + ', isLoaded = ' + str(scene['isLoaded']).lower() + ', isActive = ' + str(scene['isActive']).lower() + ' }' for scene in setup)
                evaluate('UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(new UnityEditor.SceneManagement.SceneSetup[] { ' + scenes + ' });')
        except Exception as e:
            rows[-1]['errors'].append('Scene restoration failed: ' + str(e)); rows[-1]['passed'] = False
for row in rows:
    median = 'unavailable' if row['medianSeconds'] is None else f'{row["medianSeconds"]:.3f} s'
    print(f'{row["gate"]:25} {median:>14}  <= {row["budgetSeconds"]:2} s  {"PASS" if row["passed"] else "FAIL"}')
    for error in row['errors']: print('  ' + error)
(logs / 'results.json').write_text(json.dumps(rows, indent=2) + '\n')
sys.exit(0 if all(r['passed'] for r in rows) else 1)
PY
