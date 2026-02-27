import pytest

from galaxy.api.errors import UnknownError

from local_games import parse_map_crc_for_total_size


def test_parse_map_crc_for_total_size_no_file():
    with pytest.raises(FileNotFoundError):
        parse_map_crc_for_total_size('nonexisting/path')


@pytest.mark.parametrize('content, expected', [
    (
        '', 0
    ),
    (
        '?crc=3156175984&file=game.exe&id=aeae3e851c66&jobid=%7b1b69c729-23b7-415f-a252-8f9cd9387475%7d&size=270071'
        , 270071
    ),
    (
        '?crc=3156175984&file=game.exe&id=aeae3e851c66&jobid=%7b1b69c729-23b7-415f-a252-8f9cd9387475%7d&size=10000000\n'
        '?crc=3156175984&file=game_launcher.exe&id=aeae3e851c66&jobid=%7b1b69c729-23b7-415f-a252-8f9cd9387475%7d&size=1000000'
        , 11000000
    ),
    (
        '?crc=1827740508&file=vpk%2ffrontend.bsp.pak000_dir.vpk&id=c72a35f78b00&jobid=%7bd287f304-bdb3-49ba-9ff9-c4c1a60217ee%7d&size=1000\n'
        '?crc=4211709804&file=vpk%2fmp_common.bsp.pak000_dir.vpk&id=c72a35f78b00&jobid=%7bd287f304-bdb3-49ba-9ff9-c4c1a60217ee%7d&size=10\n'
        '?crc=254416060&file=__installer%2fdlc%2favatars%2fsupport%2fmnfst.txt&id=f9eef5faa86c&jobid=%7bf1a367f9-c088-40bc-893a-c5350e04debd%7d&size=5\n'
        , 1015
    )
])
def test_parse_map_crc_for_total_size(content, expected, tmp_path):
    crc_file = tmp_path / 'map.crc'
    crc_file.write_text(content, encoding='utf-16-le')
    assert expected == parse_map_crc_for_total_size(crc_file)


@pytest.mark.asyncio
async def test_plugin_local_size_game_not_installed(plugin):
    game_id = 'gameId'
    context = await plugin.prepare_local_size_context([game_id])
    with pytest.raises(UnknownError):
        await plugin.get_local_size(game_id, context)


@pytest.mark.asyncio
@pytest.mark.parametrize('game_id_raw, game_id', [
    pytest.param("OFB-EAST%3a48217", "OFB-EAST:48217", id="origin offer id"),
    pytest.param("OFB-EAST%3a48217%40epic", "OFB-EAST:48217@epic", id="id with external type"),
])
async def test_plugin_local_size_game_installed(tmpdir, plugin, game_id_raw, game_id):
    expected_size = 142342
    local_content_dir = tmpdir.mkdir("GameName1")
    mfst_file = local_content_dir.join("Origin.gameId.mfst")
    mfst_file.write(f"?currentstate=kReadyToStart&id={game_id_raw}&previousstate=kCompleted")
    crc_file = local_content_dir.join("map.crc")
    crc_file.write_text(
        f'?crc=3156175984&file=game.exe&id=aeae3e851c66&jobid=%7b1b69c729-23b7-415f-a252-8f9cd9387475%7d&size={expected_size}',
        encoding='utf-16-le'
    )

    await plugin.get_local_games()  # need to prepare local client cache
    context = await plugin.prepare_local_size_context([game_id])
    result = await plugin.get_local_size(game_id, context)
    assert result == expected_size
