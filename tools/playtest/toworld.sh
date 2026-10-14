#!/bin/zsh
# DEV-018: Boot -> real menus -> opening dialog input -> Flight.
# The real menu owns its default seed (SPEC-61); it currently has no seed selector.
. "$(dirname "$0")/_env.sh"
python3 - "$PROJECT_ROOT" "$@" <<'PYTHON'
import pathlib, subprocess, sys, time
root = pathlib.Path(sys.argv[1])
if len(sys.argv) > 2:
    print('failed: the real menu has no seed selector; omit the seed argument to use its normal default')
    sys.exit(1)
deadline = time.monotonic() + 60
def fail(message):
    print('failed:', message)
    sys.exit(1)
def call(command, *args):
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        fail('menu entry did not reach Flight within 60 s')
    limit = min(10, remaining)
    try:
        result = subprocess.run([str(root/'tools/playtest/to.sh'), str(limit), str(root/'tools/playtest/g.sh'), command, *args],
                                capture_output=True, text=True, timeout=limit)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        output = result.stdout.strip()
        if not output:
            raise RuntimeError('empty CLI response')
        return output
    except (subprocess.TimeoutExpired, RuntimeError, OSError) as error:
        print('Editor unreachable (is a modal dialog open?):', str(error)[:300])
        sys.exit(3)
def wait(expression, seconds, message):
    until = min(deadline, time.monotonic() + seconds)
    while time.monotonic() < until:
        result = call('until', '--cond', expression)
        if result == 'true': return
        if result not in ('false', 'dev runtime not available', 'not playing'): fail(result)
        time.sleep(.1)
    fail(message)
# Only enter Play here; the submitted menu owns campaign creation.
result = call('eval', '--code',
    'if (UnityEditor.EditorApplication.isPlaying) return "already playing"; '
    'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Boot.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); '
    'UnityEditor.EditorApplication.isPlaying = true; return "boot requested";')
if result not in ('already playing', 'boot requested'): fail(result)
wait('services.ready && scene == "MainMenu"', 15, 'MainMenu is not ready')
wait('ui.visible("New game")', 5, 'New Game menu is not visible; HUD-002 must bind the real MainMenu root')
result = call('click', '--target', '#main-menu-new-game')
if not result.startswith('clicked '): fail(result)
wait('ui.visible("Commander name")', 5, 'New Game name prompt is not ready')
# Public UI field assignment raises normal validation/change callbacks; submit the real button.
result = call('eval', '--code',
    'if (!Game.Core.Services.TryGet<UnityEngine.UIElements.UIDocument>(out var document)) return "UI root not available"; '
    'var field = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.TextField>(document.rootVisualElement, "text-prompt-field"); '
    'if (field == null) return "name field not available"; field.value = "Harness"; return "name entered";')
if result != 'name entered': fail(result)
result = call('click', '--target', '#text-prompt-ok')
if not result.startswith('clicked '): fail(result)
while time.monotonic() < deadline:
    ready = call('until', '--cond', 'context == "Flight" && player.ready')
    if ready == 'true':
        print(call('snapshot'))
        sys.exit(0)
    if ready != 'false': fail(ready)
    dialog = call('until', '--cond', 'context == "Dialog"')
    if dialog == 'true':
        result = call('key', '--keys', 'Digit1', '--hold', '0.1')
        if result.startswith('failed:'): fail(result)
    elif dialog != 'false': fail(dialog)
    time.sleep(.2)
fail('menu entry did not reach Flight within 60 s')
PYTHON
