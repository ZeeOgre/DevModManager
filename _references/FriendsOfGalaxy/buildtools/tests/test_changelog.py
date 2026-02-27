from unittest.mock import patch, mock_open
import pytest

from src.fog.buildtools import Changelog


def test_changelog_init_check_for_version():
    with pytest.raises(RuntimeError):
        Changelog({"0.1": "ver01"}, check_for_version="0.2")


def test_changelog_init_check_for_version_empty():
    with pytest.raises(RuntimeError):
        Changelog({}, check_for_version="0.2")


def test_changelog_init_check_for_version_invalid():
    with pytest.raises(Exception):
        Changelog(None, check_for_version="0.2")


def test_changelog_to_markdown():
    changelog = {
        "0.1.1": "- fix1\n- fix2",
        "0.1.0": "another log"
    }
    expected = """# Changelog

## 0.1.1
- fix1
- fix2

## 0.1.0
another log

"""
    assert Changelog(changelog).to_markdown() == expected



def test_changelog_to_markdown_indents():
    changelog = {
        "0.1": """
            - fix1
            - fix2
        """
    }
    expected = """# Changelog

## 0.1
- fix1
- fix2

"""
    assert Changelog(changelog).to_markdown() == expected


def test_changelog_for_version():
    changelog = {
        "0.1": "ver01",
        "0.2": """
            ver02
        """
    }
    c = Changelog(changelog)
    assert c.for_version("0.1") == "ver01"
    assert c.for_version("0.2") == "ver02"
    with pytest.raises(TypeError):
        c.for_version(1)  # int
    with pytest.raises(KeyError):
        c.for_version("0.3")
