#!/usr/bin/env python3
"""Run the installed Pipeline test API and export its results as NUnit-shaped XML for our summary tools."""
import json
import pathlib
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

root, mode, name_filter, category, output = sys.argv[1:]
log = pathlib.Path(root) / 'Logs/tools' / ('test-' + pathlib.Path(output).stem + '.json')

def call(name, *args):
    proc = subprocess.run(['unity', 'command', name, *args, '--project-path', root, '--format', 'json', '--timeout', '60'], capture_output=True, text=True, timeout=70)
    if proc.returncode: raise RuntimeError(proc.stdout[-1500:] + proc.stderr[-500:])
    reply = json.loads(proc.stdout)
    if not reply.get('success'): raise RuntimeError(str(reply.get('errors')))
    result = reply['data']['result']
    if isinstance(result, dict) and result.get('success') is False: raise RuntimeError(str(result))
    return result

try:
    if category and name_filter:
        raise RuntimeError('The installed live test API supports one filter. Close the Editor for a combined name/category run.')
    started = call('run_tests', '--mode', mode.lower(), '--filter', category or name_filter,
                   '--filter_type', 'category' if category else 'testName', '--async_tests', 'true')
    log.write_text(json.dumps(started, indent=2) + '\n')
    deadline = time.monotonic() + 1800
    while time.monotonic() < deadline:
        result = call('test_status')
        status = result.get('status')
        if status == 'completed': break
        if status in ('error', 'cancelled', 'no_tests'): raise RuntimeError(str(result))
        time.sleep(.2)
    else:
        call('cancel_tests')
        raise RuntimeError('Test run exceeded 1800 seconds.')
    log.write_text(json.dumps(result, indent=2) + '\n')
    cases = result.get('results', [])
    if not cases: raise RuntimeError('No test cases were returned.')
    summary = result['summary']
    if len(cases) != summary['total']: raise RuntimeError('Incomplete test result list.')
    xml = ET.Element('test-run', {'duration': str(result['duration']), 'total': str(summary['total'])})
    for test in cases:
        case = ET.SubElement(xml, 'test-case', {'fullname': test['FullName'], 'result': test['Status'], 'duration': str(test['Duration'])})
        if test['Status'] == 'Failed':
            failure = ET.SubElement(case, 'failure')
            ET.SubElement(failure, 'message').text = test.get('Message', '')
            ET.SubElement(failure, 'stack-trace').text = test.get('StackTrace', '')
    ET.ElementTree(xml).write(output, encoding='utf-8', xml_declaration=True)
except Exception as error:
    print('Live Unity test run failed:', error)
    sys.exit(1)
