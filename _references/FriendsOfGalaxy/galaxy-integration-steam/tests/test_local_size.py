from textwrap import dedent

import pytest


@pytest.fixture()
def mock_get_library_folders(mocker):
    return mocker.patch("plugin.get_library_folders")


@pytest.fixture()
def mock_vdf_reader(mocker):
    return mocker.patch("plugin.load_vdf")


@pytest.mark.asyncio
async def test_import_no_appmanifest(plugin):
    context = {
        "1": "mock_path",
        "13": "mock_path"
    }
    assert await plugin.get_local_size(42, context) == 0


@pytest.mark.asyncio
@pytest.mark.parametrize('state_flags', [
    "4", "6", "132", "134"
])
async def test_import_installed(state_flags, plugin, mock_vdf_reader):
    context = {
        "111": "path_to_manifest/appmanifest_111.acf",
    }
    mock_vdf_reader.return_value = {
        "AppState": {
            "StateFlags": state_flags,
            "SizeOnDisk": "23052001786"
        }
    }
    assert await plugin.get_local_size('111', context) == 23052001786


@pytest.mark.asyncio
@pytest.mark.parametrize('state_flags', [
    "1", "2", "130", "530"
])
async def test_import_not_fully_installed(state_flags, plugin, mock_vdf_reader):
    context = {
        "111": "path_to_manifest/appmanifest_111.acf",
   }
    mock_vdf_reader.return_value = {
        "AppState": {
            "StateFlags": state_flags,
            "SizeOnDisk": "0",
            "BytesDownloaded": "1000"
        }
    }
    assert await plugin.get_local_size('111', context) == 1000


@pytest.mark.asyncio
async def test_import_invalid_manifest(
    mock_get_library_folders,
    plugin,
    tmpdir
):
    app_id = '440'
    mock_dir = tmpdir.mkdir('dir1')
    acf1 = mock_dir.join(f"appmanifest_{app_id}.acf")
    acf1.write(b'\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0')
    mock_get_library_folders.return_value = [str(mock_dir)]

    context = await plugin.prepare_local_size_context([app_id])
    assert await plugin.get_local_size(app_id, context) == None


@pytest.mark.asyncio
async def test_full(
    mock_get_library_folders,
    plugin,
    tmpdir
):
    data = '''\
    "AppState"
    {
        "appid"		"440"
        "Universe"		"1"
        "name"		"Team Fortress 2"
        "StateFlags"		"4"
        "installdir"		"Team Fortress 2"
        "LastUpdated"		"1579680835"
        "UpdateResult"		"0"
        "SizeOnDisk"		"23052001786"
        "buildid"		"4584287"
        "LastOwner"		"76561198058637796"
        "BytesToDownload"		"3762624"
        "BytesDownloaded"		"3762624"
        "AutoUpdateBehavior"		"0"
        "AllowOtherDownloadsWhileRunning"		"0"
        "ScheduledAutoUpdate"		"0"
        "InstalledDepots"
        {
            "441"
            {
                "manifest"		"2746557701761425532"
            }
            "440"
            {
                "manifest"		"1118032470228587934"
            }
            "232251"
            {
                "manifest"		"152106512586891480"
            }
        }
        "MountedDepots"
        {
            "441"		"2746557701761425532"
            "440"		"1118032470228587934"
            "232251"		"152106512586891480"
        }
        "SharedDepots"
        {
            "228990"		"228980"
        }
        "UserConfig"
        {
            "language"		"english"
        }
    }
    '''
    app_id = '440'
    dir1 = tmpdir.mkdir('dir1')
    acf1 = dir1.join(f"appmanifest_{app_id}.acf")
    mock_get_library_folders.return_value = [str(dir1)]
    acf1.write_text(dedent(data), encoding='utf-8')
    context = await plugin.prepare_local_size_context([app_id])
    assert await plugin.get_local_size(app_id, context) == 23052001786

