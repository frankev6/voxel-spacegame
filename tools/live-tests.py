#!/usr/bin/env python3
"""Run tests in the open Editor with project ownership and exact result-inventory validation. EditMode goes through Pipeline's
run_tests; PlayMode goes through Unity's TestRunnerApi (Game.DevTools.Editor.Tests.LiveTestRun, DEV-026), because Pipeline entered
Play before the runner's first task and every live PlayMode run failed ("This cannot be used during play mode")."""
from collections import Counter
import json
import pathlib
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from unity_tool_lock import ProjectLock


def select_tests(listing, mode, name_filter, category):
    if listing.get('Mode', '').casefold() != mode.casefold():
        raise RuntimeError('Test inventory mode does not match the requested mode.')
    tests = listing.get('Tests', [])
    if len(tests) != listing.get('Count'):
        raise RuntimeError('Incomplete test inventory.')
    selected = []
    for test in tests:
        if test.get('Mode', '').casefold() != mode.casefold():
            raise RuntimeError('Test inventory contains another mode.')
        if test.get('Explicit'):
            continue
        if name_filter and name_filter.casefold() not in test['FullName'].casefold():
            continue
        categories = {c.casefold() for c in test.get('Categories', [])}
        terms = [c.casefold() for c in category.split(';') if c]
        included = [c for c in terms if not c.startswith('!')]
        excluded = [c[1:] for c in terms if c.startswith('!')]
        if (included and not categories.intersection(included)) or categories.intersection(excluded):
            continue
        selected.append(test['FullName'])
    if not selected:
        raise RuntimeError('No non-explicit tests match the requested mode/filter.')
    return Counter(selected)


def validate_results(result, expected):
    if result.get('status') != 'completed':
        raise RuntimeError('Test run is not complete.')
    cases = result.get('results', [])
    actual = Counter(test['FullName'] for test in cases)
    if actual != expected:
        missing = list((expected - actual).elements())[:3]
        unexpected = list((actual - expected).elements())[:3]
        raise RuntimeError(f'Test result identity mismatch; missing {missing}; unexpected {unexpected}. No XML exported.')
    statuses = Counter(test['Status'].casefold() for test in cases)
    known = ('passed', 'failed', 'skipped', 'inconclusive')
    if set(statuses) - set(known):
        raise RuntimeError('Unknown test result status.')
    summary = result.get('summary', {})
    if summary.get('total') != len(cases) or any(summary.get(key, 0) != statuses[key] for key in known):
        raise RuntimeError('Test result summary does not match its cases.')
    return cases


def run(root, mode, name_filter, category, output):
    log = pathlib.Path(root) / 'Logs/tools' / ('test-' + pathlib.Path(output).stem + '.json')

    def call(name, *args):
        proc = subprocess.run(['unity', 'command', name, *args, '--project-path', root, '--format', 'json', '--timeout', '60'], capture_output=True, text=True, timeout=70)
        if proc.returncode:
            raise RuntimeError(proc.stdout[-1500:] + proc.stderr[-500:])
        reply = json.loads(proc.stdout)
        if not reply.get('success'):
            raise RuntimeError(str(reply.get('errors')))
        result = reply['data']['result']
        if isinstance(result, dict) and result.get('success') is False:
            raise RuntimeError(str(result))
        return result

    exclusions = category.startswith('!')
    if category and name_filter and not exclusions:
        raise RuntimeError('The installed live test API supports one filter. Close the Editor for a combined name/category run.')
    # An EditMode run cannot set up its scene while the Editor plays: it waited forever (a playtest had left Play on). Stop Play first.
    if mode.casefold() == 'editmode':
        editor = call('editor_status')
        if isinstance(editor, dict) and editor.get('playMode') not in (None, 'stopped'):
            call('editor_stop')
            for _ in range(150):
                editor = call('editor_status')
                if isinstance(editor, dict) and editor.get('playMode') == 'stopped' and not editor.get('domainReloadInProgress'):
                    break
                time.sleep(.2)
            else:
                raise RuntimeError('The Editor did not leave Play mode; no tests dispatched.')
    status = call('test_status')
    if isinstance(status, dict) and status.get('status') == 'running':
        raise RuntimeError('Unity already has an active test request. No replacement request dispatched.')
    if mode.casefold() == 'playmode':
        return run_direct(root, mode, name_filter, category, output, call, log)
    listing = call('list_tests', '--mode', mode.lower())
    expected = select_tests(listing, mode, name_filter, category)
    dispatch_category = category
    if exclusions:
        # Pipeline only supports literal name/category filters, not exclusion expressions.
        # A focused ordinary selection is safe only when its inventory is identical.
        if expected != select_tests(listing, mode, name_filter, ''):
            raise RuntimeError('Live API cannot exclude benchmarks/soaks from this selection. Use a focused functional name filter, or run headlessly. No tests dispatched.')
        dispatch_category = ''
    request = dict(mode=mode, nameFilter=name_filter, category=category, inventory=sorted(expected.elements()))
    pathlib.Path(output).unlink(missing_ok=True)
    receipt = call('run_tests', '--mode', mode.lower(), '--filter', dispatch_category or name_filter,
                   '--filter_type', 'category' if dispatch_category else 'testName', '--async_tests', 'true')
    log.write_text(json.dumps(dict(request=request, receipt=receipt), indent=2) + '\n')
    if receipt.get('Mode', '').casefold() != mode.casefold():
        raise RuntimeError('Test dispatch receipt mode does not match request.')
    deadline = time.monotonic() + 1800
    checked = time.monotonic()
    while time.monotonic() < deadline:
        result = call('test_status')
        status = result.get('status')
        if status == 'completed':
            break
        if status in ('error', 'cancelled', 'no_tests'):
            raise RuntimeError(str(result))
        if mode.casefold() == 'editmode' and time.monotonic() - checked > 10:
            checked = time.monotonic()
            editor = call('editor_status')
            if isinstance(editor, dict) and editor.get('playMode') not in (None, 'stopped'):
                call('cancel_tests')
                raise RuntimeError('The Editor entered Play mode during an EditMode run, which cannot finish under it: cancelled. Stop Play and run again.')
        time.sleep(.2)
    else:
        # No server run ID exists: do not cancel a possibly replaced external request.
        raise RuntimeError('Test run exceeded 1800 seconds; it may still be running in Unity. No replacement request sent.')
    log.write_text(json.dumps(dict(result, request=request, receipt=receipt), indent=2) + '\n')
    cases = validate_results(result, expected)
    xml = ET.Element('test-run', {'duration': str(result['duration']), 'total': str(len(cases)), 'mode': mode,
                                 'name-filter': name_filter, 'category': category})
    for test in cases:
        case = ET.SubElement(xml, 'test-case', {'fullname': test['FullName'], 'result': test['Status'], 'duration': str(test['Duration'])})
        if test['Status'] == 'Failed':
            failure = ET.SubElement(case, 'failure')
            ET.SubElement(failure, 'message').text = test.get('Message', '')
            ET.SubElement(failure, 'stack-trace').text = test.get('StackTrace', '')
    ET.ElementTree(xml).write(output, encoding='utf-8', xml_declaration=True)


def run_direct(root, mode, name_filter, category, output, call, log):
    """PlayMode through TestRunnerApi: exactly the inventory's tests, the runner's NUnit XML at `output`, then the same identity
    check as the Pipeline path. Never stops a Play session it did not start."""
    editor = call('editor_status')
    if isinstance(editor, dict) and editor.get('playMode') not in (None, 'stopped'):
        raise RuntimeError('The Editor is in Play; stop it (or wait for the playtest) and run again. Not stopped for you.')
    listing = call('list_tests', '--mode', mode.lower())
    expected = select_tests(listing, mode, name_filter, category)
    names = pathlib.Path(root) / 'Logs/tools/live-test-names.txt'
    names.parent.mkdir(parents=True, exist_ok=True)
    names.write_text('\n'.join(sorted(expected)) + '\n', encoding='utf-8')
    status_path = pathlib.Path(root) / 'Logs/tools/live-test-status.json'
    try:
        if json.loads(status_path.read_text(encoding='utf-8')).get('state') == 'running' and time.time() - status_path.stat().st_mtime < 1800:
            raise RuntimeError('A live TestRunnerApi run is still going (Logs/tools/live-test-status.json). No replacement request dispatched.')
    except (OSError, ValueError):
        pass
    status_path.unlink(missing_ok=True)
    pathlib.Path(output).unlink(missing_ok=True)
    request = dict(mode=mode, nameFilter=name_filter, category=category, inventory=sorted(expected.elements()), runner='TestRunnerApi')
    started = call('eval', f'return Game.DevTools.Editor.Tests.LiveTestRun.Start("{mode}", "{names}", "{pathlib.Path(output).resolve()}");')
    if isinstance(started, dict):
        started = started.get('result')
    log.write_text(json.dumps(dict(request=request, receipt=started), indent=2) + '\n')
    if started != 'started':
        raise RuntimeError('Live PlayMode run not started: ' + str(started))
    deadline = time.monotonic() + 1800
    state = {}
    while time.monotonic() < deadline:
        time.sleep(1)
        try:
            state = json.loads(status_path.read_text(encoding='utf-8'))
        except (OSError, ValueError):
            continue
        if state.get('state') in ('completed', 'error'):
            break
    else:
        raise RuntimeError('Live PlayMode run exceeded 1800 seconds; it may still be running in Unity.')
    log.write_text(json.dumps(dict(request=request, receipt=started, status=state), indent=2) + '\n')
    if state.get('state') != 'completed':
        raise RuntimeError('Live PlayMode run failed: ' + str(state.get('message')))
    cases = [c.get('fullname') for c in ET.parse(output).getroot().iter('test-case')]
    if Counter(cases) != expected:
        missing = list((expected - Counter(cases)).elements())[:3]
        unexpected = list((Counter(cases) - expected).elements())[:3]
        raise RuntimeError(f'Test result identity mismatch; missing {missing}; unexpected {unexpected}.')


def main():
    root, mode, name_filter, category, output = sys.argv[1:]
    try:
        with ProjectLock(root, 'live tests ' + mode):
            run(root, mode, name_filter, category, output)
        return 0
    except Exception as error:
        print('Live Unity test run failed:', error)
        return 1


if __name__ == '__main__':
    sys.exit(main())
