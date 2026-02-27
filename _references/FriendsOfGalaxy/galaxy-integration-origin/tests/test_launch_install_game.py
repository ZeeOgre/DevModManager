from unittest.mock import Mock
import pytest


@pytest.fixture
def browser_open(mocker):
    return mocker.patch("webbrowser.open", autospec=True)


@pytest.fixture
def uri_handler_installed(mocker):
    return mocker.patch("plugin.is_uri_handler_installed", autospec=True)


@pytest.fixture
def authenticated_plugin(authenticated_plugin, uri_handler_installed):
    """Wrapper fixture with patched Origin uri handler installed by default"""
    uri_handler_installed.return_value = True
    return authenticated_plugin


@pytest.mark.asyncio
@pytest.mark.parametrize("game_id", ["OFR.123", "OFR.123@steam"])
@pytest.mark.parametrize("command", ["launch_game", "install_game"])
async def test_command_when_origin_not_installed(
    browser_open,
    authenticated_plugin,
    backend_client,
    uri_handler_installed,
    game_id,
    command,
):
    uri_handler_installed.return_value = False
    expected_uri = "https://www.origin.com/download"

    command_method = getattr(authenticated_plugin, command)
    await command_method(game_id)
    backend_client.get_entitlements.assert_not_called()
    browser_open.assert_called_once_with(expected_uri)


@pytest.mark.asyncio
async def test_launch_when_origin_installed(browser_open, authenticated_plugin, backend_client):
    LAUNCH_URI = "origin2://game/launch?offerIds={}&autoDownload=1"
    game_id = "Origin.OFR.50.000252@steam"
    expected_uri = LAUNCH_URI.format(game_id)

    await authenticated_plugin.launch_game(game_id)
    backend_client.get_entitlements.assert_not_called()
    browser_open.assert_called_once_with(expected_uri)


@pytest.mark.asyncio
@pytest.mark.parametrize("game_id", ["Origin.OFR.50.0001051" "Origin.OFR.50.0001051@epic"])
async def test_install_game(authenticated_plugin, browser_open, game_id):
    expected_uri = f"origin2://game/download?offerId={game_id}"

    await authenticated_plugin.install_game(game_id)
    browser_open.assert_called_once_with(expected_uri)


@pytest.mark.asyncio
async def test_install_activated_subscription_game(authenticated_plugin, browser_open, mocker):
    offer_id = "Origin.OFR.50.0001051"
    game_id = "Origin.OFR.50.0001051@subscription"
    expected_uri = f"origin2://game/download?offerId={game_id}"

    mocker.patch.object(
        type(authenticated_plugin),
        "persistent_cache",
        new_callable=mocker.PropertyMock,
        return_value={"offers": {offer_id: Mock(dict)}},
    )

    await authenticated_plugin.install_game(game_id)
    browser_open.assert_called_once_with(expected_uri)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "offer, expected_uri",
    [
        pytest.param(
            {"gdpPath": "madden/madden-21"},
            # there is origin2://store/open but I didn't found a way to open exact game view
            # in local origin client, thus opening web store client
            "https://www.origin.com/store/madden/madden-21",
        ),
        pytest.param(
            {},
            "https://www.origin.com/store/ea-play/play-list",
            id="no gdpPath in offer json",
        ),
    ],
)
async def test_install_not_activated_subscription_game(
    authenticated_plugin,
    backend_client,
    browser_open,
    offer,
    expected_uri,
):
    offer_id = "Origin.OFR.50.0003744"
    game_id = f"{offer_id}@subscription"
    backend_client.get_offer.return_value = offer

    await authenticated_plugin.install_game(game_id)
    browser_open.assert_called_once_with(expected_uri)
    backend_client.get_offer.assert_called_once_with(offer_id)
