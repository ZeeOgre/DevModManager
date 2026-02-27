import os
from src.fog.buildtools import chdir


def test_chdir_dir():
    path = os.path.abspath(os.path.join('..', '..'))
    with chdir(path):
        assert os.getcwd() == path


def test_chdir_file():
    with chdir(__file__):
        assert os.getcwd() == os.path.abspath(os.path.dirname(__file__))