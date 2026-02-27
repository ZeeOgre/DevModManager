import os
import sys
import glob
import shutil
import pathlib
import tempfile
import shlex
import subprocess
import contextlib
from typing import Dict, Optional, Any, Tuple

from .changelog import Changelog


RELEASE_FILE ="current_version.json"


def _run(*args, **kwargs):
    cmd = list(args)
    if len(cmd) == 1:
        cmd = shlex.split(cmd[0])
    kwargs.setdefault("capture_output", True)
    kwargs.setdefault("text", True)
    print('executing', cmd)
    out = subprocess.run(cmd, **kwargs)
    try:
        out.check_returncode()
    except subprocess.CalledProcessError as e:
        err_str = f'{e.output}\n{e.stderr}'
        print('><', err_str)
        raise e
    else:
        print('>>', out.stdout)
    return out


@contextlib.contextmanager
def chdir(to):
    path = pathlib.Path(to)
    if path.is_file():  # probably mean the file directory
        path = path.parent.resolve()
    else:
        path = path.resolve()

    pwd = os.getcwd()
    os.chdir(path)
    try:
        yield str(path)
    finally:
        os.chdir(pwd)


def build(src='src', output='build', third_party_output='.', requirements='requirements/app.txt', python_version='37'):
    """Builds a plugin

    Args:
        src: Path to the source directory (relative to current directory)
        output: Path to the output directory (relative to current directory)
        third_party_output: Path to the third party output directory (relative to output)
        requirements: Path to the requirements file (relative to src)
        python_version: Python version to use (default: 3.7)
    """

    src_path = pathlib.Path(src).resolve()
    out_path = pathlib.Path(output).resolve()
    req_path = pathlib.Path(requirements)

    try:
        out_path.relative_to(src_path)
    except ValueError:
        pass
    else:
        raise RuntimeError("dist (output) cannot be part of src")

    if out_path.exists():
        shutil.rmtree(out_path)

    to_ignore = shutil.ignore_patterns(RELEASE_FILE, '.*', 'test_*.py', '*_test.py', '*.pyc', '__pycache__')
    shutil.copytree(src_path, output, ignore=to_ignore)

    if sys.platform == "win32":
        pip_platform = "win32"
    elif sys.platform == "darwin":
        pip_platform = "macosx_10_13_x86_64"
    else:
        raise RuntimeError(f'Platform {sys.platform} not supported')
    pip_target = (out_path / third_party_output).as_posix()

    try:
        with tempfile.NamedTemporaryFile(mode="w", delete=False) as tmp:
            _run(f'pip-compile {req_path.as_posix()} --output-file=-', stdout=tmp, stderr=subprocess.PIPE, capture_output=False)
            _run('pip', 'install',
                '-r', tmp.name,
                '--platform', pip_platform,
                '--target', pip_target,
                '--python-version', python_version,
                '--no-compile',
                '--no-deps'
            )
    finally:
        os.unlink(tmp.name)

    # cleaning up dist directories and tests
    for dir_ in glob.glob(f"{str(out_path)}/*.dist-info"):
        shutil.rmtree(dir_)
    for test in glob.glob(f"{str(out_path)}/**/test_*.py", recursive=True):
        os.remove(test)
    for test in glob.glob(f"{str(out_path)}/**/*_test.py", recursive=True):
        os.remove(test)


def _load_version_module(repo_path) -> Tuple[str, Dict[str, Any]]:
    """Helper used in fog plugins due to avoid relative import problems"""
    version_file = os.path.join(repo_path, "src", "version.py")
    version = {}
    exec(open(version_file).read(), version)
    return version


def load_version(repo_path):
    return _load_version_module(repo_path)['__version__']


def update_changelog_file(repo_path, out_dir=os.getcwd()):
    """Helper function to update changelog file"""
    version_content = _load_version_module(repo_path)
    version = version_content['__version__']
    changelog = version_content.get('__changelog__')
    if not changelog:  # do not allow for accidental wipe out
        raise TypeError('Changelog cannot be empty!')

    c = Changelog(changelog, check_for_version=version)
    with open(os.path.join(out_dir, 'CHANGELOG.md'), 'w') as f:
        f.write(c.to_markdown())
