import platform

import pytest

from uri_scheme_handler import is_uri_handler_installed


@pytest.mark.skipif(platform.system().lower() != "windows", reason="windows registry")
@pytest.mark.parametrize(
    "reg_value_template",
    [
        pytest.param('{} "%1"', id="Origin case"),
        pytest.param('"{}" "%1"', id="EA Desktop case"),
        pytest.param('{} "%1" "%2"', id="more params (hypotetical)"),
        pytest.param("{} %1", id="no quotes around param (hypotetical)"),
        pytest.param("{}", id="just exe without params (hypotetical)"),
    ],
)
def test_win_reg_uri_handler_installed(mocker, reg_value_template):
    launcher_path = r"C:\Program Files\EA Desktop Example Path\EALauncher.exe"
    reg_value = reg_value_template.format(launcher_path)

    mocker.patch("winreg.OpenKey")
    mocker.patch("winreg.QueryValue", return_value=reg_value)
    mocker.patch("os.path.exists", side_effect=lambda x: x == launcher_path)

    assert is_uri_handler_installed(mocker.Mock()) == True


@pytest.mark.skipif(platform.system().lower() != "windows", reason="windows registry")
@pytest.mark.parametrize(
    "problem_patch_config",
    [
        {"target": "winreg.OpenKey", "side_effect": OSError},
        {"target": "winreg.OpenKey", "side_effect": FileNotFoundError},
        {"target": "winreg.QueryValue", "side_effect": PermissionError},
        {"target": "os.path.exists", "return_value": False},
    ],
)
def test_win_reg_uri_hanlder_not_installed(mocker, problem_patch_config):
    # "installed" case patches
    mocker.patch("winreg.OpenKey")
    mocker.patch("winreg.QueryValue", return_value="path"),
    mocker.patch("os.path.exists", return_value=True)
    assert is_uri_handler_installed(mocker.Mock()) == True, "test preconfiguration failed"

    # problem patch
    mocker.patch(**problem_patch_config)

    assert is_uri_handler_installed(mocker.Mock()) == False
