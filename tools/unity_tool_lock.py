#!/usr/bin/env python3
"""Project-local OS lock for tools that mutate the single Unity Editor/test channel."""
import fcntl
import json
import os
import pathlib
import subprocess
import sys
from contextlib import AbstractContextManager

OWNER_ENV = 'GAME_UNITY_TOOL_LOCK_ROOT'
FD_ENV = 'GAME_UNITY_TOOL_LOCK_FD'


class ProjectLock(AbstractContextManager):
    def __init__(self, root, label):
        self.root = str(pathlib.Path(root).resolve())
        self.label = label
        self.stream = None
        self.previous = {}
        self.fd = None

    def __enter__(self):
        if os.environ.get(OWNER_ENV) == self.root:
            # Compile/test children of iteration-gates reuse its ownership.
            inherited = os.environ.get(FD_ENV)
            try:
                self.fd = int(inherited)
                os.fstat(self.fd)
            except (TypeError, ValueError, OSError):
                self.fd = None  # A waiting ancestor still owns the lock.
            return self
        path = pathlib.Path(self.root) / 'Logs/tools/unity-tool.lock'
        path.parent.mkdir(parents=True, exist_ok=True)
        self.stream = path.open('a+')
        try:
            fcntl.flock(self.stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            self.stream.seek(0)
            owner = self.stream.read(500).strip()
            self.stream.close()
            self.stream = None
            raise RuntimeError('Another Unity tool operation owns this project; no command dispatched. ' + owner)
        self.fd = self.stream.fileno()
        self.stream.seek(0)
        self.stream.truncate()
        json.dump(dict(pid=os.getpid(), label=self.label), self.stream)
        self.stream.flush()
        self.previous = {key: os.environ.get(key) for key in (OWNER_ENV, FD_ENV)}
        os.environ[OWNER_ENV] = self.root
        os.environ[FD_ENV] = str(self.fd)
        return self

    def __exit__(self, *args):
        if self.stream is not None:
            for key, value in self.previous.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value
            self.stream.close()  # Do not unlink: waiters must always lock the same inode.
        return False


def main():
    if len(sys.argv) < 4 or sys.argv[2] != '--':
        print('usage: unity_tool_lock.py <project> -- <command> [args]', file=sys.stderr)
        return 2
    command = sys.argv[3:]
    try:
        with ProjectLock(sys.argv[1], pathlib.Path(command[0]).name) as lock:
            # Keep ownership in the direct child too if this wrapper is killed.
            return subprocess.run(command, pass_fds=(() if lock.fd is None else (lock.fd,))).returncode
    except (RuntimeError, OSError) as error:
        print(error, file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
