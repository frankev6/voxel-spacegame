import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from collections import Counter

TOOLS = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
from unity_tool_lock import ProjectLock, OWNER_ENV, FD_ENV
spec = importlib.util.spec_from_file_location('live_tests', TOOLS / 'live-tests.py')
live = importlib.util.module_from_spec(spec)
spec.loader.exec_module(live)


class UnityToolTests(unittest.TestCase):
    def test_competing_lock_and_process_exit_release(self):
        with tempfile.TemporaryDirectory() as root:
            env = dict(os.environ)
            env.pop(OWNER_ENV, None)
            env.pop(FD_ENV, None)
            code = f"import sys,time; sys.path.insert(0,{str(TOOLS)!r}); from unity_tool_lock import ProjectLock\nwith ProjectLock({root!r},'test owner'):\n print('owned',flush=True)\n time.sleep(30)"
            owner = subprocess.Popen([sys.executable, '-u', '-c', code], stdout=subprocess.PIPE, text=True, env=env)
            command = [sys.executable, str(TOOLS/'unity_tool_lock.py'), root, '--', sys.executable, '-c', 'print("executed")']
            try:
                self.assertEqual('owned', owner.stdout.readline().strip())
                rejected = subprocess.run(command, text=True, capture_output=True, env=env, timeout=5)
                self.assertEqual(1, rejected.returncode)
                self.assertIn('no command dispatched', rejected.stderr)
                self.assertNotIn('executed', rejected.stdout)
            finally:
                owner.terminate()
                owner.wait(timeout=5)
                owner.stdout.close()
            recovered = subprocess.run(command, text=True, capture_output=True, env=env, timeout=5)
            self.assertEqual(0, recovered.returncode, recovered.stderr)
            self.assertEqual('executed', recovered.stdout.strip())

    def test_nested_tools_reuse_ownership(self):
        with tempfile.TemporaryDirectory() as root, ProjectLock(root, 'outer'):
            result = subprocess.run([sys.executable, str(TOOLS/'unity_tool_lock.py'), root, '--', sys.executable, '-c', 'print("nested")'], text=True, capture_output=True, timeout=5)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual('nested', result.stdout.strip())

    def inventory(self):
        return dict(Mode='EditMode', Count=3, Tests=[
            dict(FullName='Edit.A', Mode='EditMode', Explicit=False, Categories=['Performance']),
            dict(FullName='Edit.B', Mode='EditMode', Explicit=False, Categories=['Other']),
            dict(FullName='Edit.Guard', Mode='EditMode', Explicit=True, Categories=['Performance'])])

    def result(self, names=('Edit.A',), passed=1):
        return dict(status='completed', summary=dict(total=len(names), passed=passed, failed=0, skipped=0, inconclusive=0),
                    results=[dict(FullName=name, Status='Passed') for name in names])

    def test_selection_matches_filter_and_excludes_explicit(self):
        self.assertEqual(Counter({'Edit.A': 1}), live.select_tests(self.inventory(), 'EditMode', '', 'performance'))
        self.assertEqual(Counter({'Edit.B': 1}), live.select_tests(self.inventory(), 'EditMode', '.b', ''))
        with self.assertRaisesRegex(RuntimeError, 'mode'):
            live.select_tests(self.inventory(), 'PlayMode', '', '')

    def test_other_mode_or_filter_results_rejected(self):
        expected = live.select_tests(self.inventory(), 'EditMode', '.a', '')
        for name in ('Play.A', 'Edit.B'):
            with self.assertRaisesRegex(RuntimeError, 'identity mismatch'):
                live.validate_results(self.result((name,)), expected)
        self.assertEqual(1, len(live.validate_results(self.result(), expected)))

    def test_active_request_preserves_previous_output(self):
        with tempfile.TemporaryDirectory() as root:
            output = pathlib.Path(root) / 'previous.xml'
            output.write_text('previous evidence')
            reply = subprocess.CompletedProcess([], 0, json.dumps(dict(success=True, data=dict(result=dict(status='running')))), '')
            with patch.object(live.subprocess, 'run', return_value=reply) as call:
                with self.assertRaisesRegex(RuntimeError, 'active test request'):
                    live.run(root, 'PlayMode', '', '', str(output))
            self.assertEqual('previous evidence', output.read_text())
            self.assertEqual(1, call.call_count)
            self.assertEqual('test_status', call.call_args.args[0][2])

    def test_inconsistent_summary_rejected(self):
        with self.assertRaisesRegex(RuntimeError, 'summary'):
            live.validate_results(self.result(passed=0), Counter({'Edit.A': 1}))


if __name__ == '__main__':
    unittest.main()
