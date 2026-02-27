from textwrap import dedent
from typing import Iterable

import pytest
from galaxy.api.errors import AuthenticationRequired
from galaxy.api.types import GameLibrarySettings

from backend import OriginBackendClient
from plugin import GameLibrarySettingsContext


@pytest.fixture()
def create_privacy_settings_xml_response(create_xml_response):
    def fn(category: str, *items: Iterable[str]):
        return create_xml_response(dedent(f'''
         <?xml version="1.0" encoding="UTF-8"?>
            <privacySettings>
               <privacySetting>
                  <userId>1008620950926</userId>
                  <category>{category}</category>
                  <payload>{";".join(items)}</payload>
               </privacySetting>
            </privacySettings>
        ''').strip())
    return fn


@pytest.fixture()
def create_hidden_games_xml_response(create_privacy_settings_xml_response):
    def fn(items: Iterable[str]):
        return create_privacy_settings_xml_response("HIDDENGAMES", *items)
    return fn


@pytest.fixture()
def create_favorites_xml_response(create_privacy_settings_xml_response):
    def fn(items: Iterable[str]):
        return create_privacy_settings_xml_response("FAVORITEGAMES", *items)
    return fn


GAME_LIBRARY_TEST_DATA = [  # (game_id, hidden, favorite)
    ('OFB-EAST:48217', False, True),
    ('OFB-EAST:109552409', True, True),
    ('DR:119971300', False, True),
    ('Origin.OFR.50.0002694@steam', True, False),
    ('OTHER', False, False)
]


@pytest.fixture
def game_ids():
    return [it[0] for it in GAME_LIBRARY_TEST_DATA]


@pytest.fixture
def favorite_games():
    return set([game_id for game_id, _, favorite in GAME_LIBRARY_TEST_DATA if favorite])


@pytest.fixture
def hidden_games():
    return  set([game_id for game_id, hidden, _ in GAME_LIBRARY_TEST_DATA if hidden])


@pytest.fixture
def game_library_context(favorite_games, hidden_games):
    return GameLibrarySettingsContext(
        favorite=favorite_games,
        hidden=hidden_games
    )


@pytest.mark.asyncio
async def test_not_authenticated(plugin, http_client):
    http_client.is_authenticated.return_value = False
    with pytest.raises(AuthenticationRequired):
        await plugin.prepare_game_library_settings_context([])


@pytest.mark.asyncio
async def test_prepare_library_settings_context(
    authenticated_plugin,
    backend_client,
    user_id,
    hidden_games,
    favorite_games,
    game_library_context,
    game_ids,
):
    backend_client.get_favorite_games.return_value = favorite_games
    backend_client.get_hidden_games.return_value = hidden_games

    assert game_library_context == await authenticated_plugin.prepare_game_library_settings_context(game_ids)

    backend_client.get_favorite_games.assert_called_once_with(user_id)
    backend_client.get_hidden_games.assert_called_once_with(user_id)


@pytest.mark.asyncio
async def test_get_favorite_games(
    user_id,
    http_client,
    create_favorites_xml_response,
    favorite_games,
):
    http_client.get.return_value = create_favorites_xml_response(favorite_games)
    backend_client = OriginBackendClient(http_client)

    assert favorite_games == await backend_client.get_favorite_games(user_id)


@pytest.mark.asyncio
async def test_get_hidden_games(
    user_id,
    http_client,
    create_hidden_games_xml_response,
    hidden_games,
):
    http_client.get.return_value = create_hidden_games_xml_response(hidden_games)
    backend_client = OriginBackendClient(http_client)

    assert hidden_games == await backend_client.get_hidden_games(user_id)


@pytest.mark.asyncio
@pytest.mark.parametrize('game_id, hidden, favorite', GAME_LIBRARY_TEST_DATA)
async def test_get_game_library_settings(
    authenticated_plugin,
    game_id, hidden, favorite,
    game_library_context
):
    tags = ['favorite'] if favorite else []
    result = await authenticated_plugin.get_game_library_settings(game_id, game_library_context)
    assert result == GameLibrarySettings(game_id, tags, hidden)


@pytest.mark.asyncio
async def test_get_game_library_settings_subscription_external_type(
    authenticated_plugin,
):
    """ 
    The privacy settings Origin API is inconsistent about externalType:
    - for subscription games offerIds are listed in the payload without @subscription suffix
    - for other `externalType`s id full id is listed e.g. OFR:22@epic
    """
    game_ids = ["OFR:123@subscription", "OFR:001@steam"]
    context = GameLibrarySettingsContext(
        favorite=set(["OFR:123", "OFR:001@steam"]),
        hidden=set(["OFR:123", "OFR:001@steam"])
    )

    tags, hidden = ['favorite'], True
    for game_id in game_ids:
        assert GameLibrarySettings(game_id, tags, hidden) == await authenticated_plugin.get_game_library_settings(game_id, context)
