from inspect import cleandoc
from typing import Dict, Optional


class Changelog:
    def __init__(self, changelog: Dict[str, str], check_for_version: Optional[str]=None):
        if check_for_version and check_for_version not in changelog:
            raise RuntimeError(f'Changelog for version [{check_for_version}] required')
        self._changelog = changelog

    def __str__(self) -> str:
        return str(self._changelog)

    def for_version(self, version: str) -> Optional[str]:
        """Gets changelog text for given `version`.
        Raises KeyError if no such changelog for `version` found
        """
        if type(version) == int:
            raise TypeError("Version should be str")
        if version not in self._changelog:
            raise KeyError(f'No changelog found for version [{version}]')

        cont = cleandoc(self._changelog.get(version, ''))
        if cont == '':
            return None
        return cont

    @property
    def _header(self) -> str:
        return "# Changelog\n\n"

    def to_markdown(self) -> str:
        """Simple implementation - do not support identifiers like [Added] [Fixed]"""
        content = self._header
        for k, v in self._changelog.items():
            content += f"## {k}\n{cleandoc(v)}\n\n"
        return content
