#!/usr/bin/env python3
"""Run Pipeline tests with project ownership and exact result-inventory validation."""
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
        if category and category.casefold() not in [c.casefold() for c in test.get('Categories', [])]:
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

    if category and name_filter:
        raise RuntimeError('The installed live test API supports one filter. Close the Editor for a combined name/category run.')
    status = call('test_status')
    if isinstance(status, dict) and status.get('status') == 'running':
        raise RuntimeError('Unity already has an active test request. No replacement request dispatched.')
    expected = select_tests(call('list_tests', '--mode', mode.lower()), mode, name_filter, category)
    request = dict(mode=mode, nameFilter=name_filter, category=category, inventory=sorted(expected.elements()))
    pathlib.Path(output).unlink(missing_ok=True)
    receipt = call('run_tests', '--mode', mode.lower(), '--filter', category or name_filter,
                   '--filter_type', 'category' if category else 'testName', '--async_tests', 'true')
    log.write_text(json.dumps(dict(request=request, receipt=receipt), indent=2) + '\n')
    if receipt.get('Mode', '').casefold() != mode.casefold():
        raise RuntimeError('Test dispatch receipt mode does not match request.')
    deadline = time.monotonic() + 1800
    while time.monotonic() < deadline:
        result = call('test_status')
        status = result.get('status')
        if status == 'completed':
            break
        if status in ('error', 'cancelled', 'no_tests'):
            raise RuntimeError(str(result))
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
