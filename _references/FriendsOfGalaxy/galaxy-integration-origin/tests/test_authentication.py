import asyncio
from http.cookies import Morsel
from unittest.mock import patch

from galaxy.api.types import Authentication, NextStep

from plugin import AUTH_PARAMS, JS


def test_no_stored_credentials(plugin, http_client, backend_client):
    loop = asyncio.get_event_loop()
    
    cookies = {
        "cookie": "value"
    }
    user_id = "13"
    persona_id = "19"
    user_name = "Jan"

    http_client.authenticate.return_value = None
    backend_client.get_identity.return_value = user_id, persona_id, user_name

    with patch.object(plugin, "store_credentials") as store_credentials:
        result = loop.run_until_complete(plugin.authenticate())
        assert result == NextStep("web_session", AUTH_PARAMS, js=JS)

        credentials = {
            "cookies": cookies,
        }

        result = loop.run_until_complete(plugin.pass_login_credentials(
            "whatever step",
            "whatever credentials",
            [{"name": key, "value": value} for key, value in cookies.items()]
        ))
        assert result == Authentication(user_id, user_name)
        store_credentials.assert_called_with(credentials)

    http_client.authenticate.assert_called_with(cookies)
    backend_client.get_identity.assert_called_with()


def test_stored_credentials(plugin, http_client, backend_client):
    loop = asyncio.get_event_loop()

    user_id = "13"
    persona_id = "19"
    user_name = "Jan"

    cookies = {
        "cookie": "value"
    }
    credentials = {
        "cookies": cookies
    }

    http_client.authenticate.return_value = None
    backend_client.get_identity.return_value = user_id, persona_id, user_name

    with patch.object(plugin, "store_credentials") as store_credentials:
        result = loop.run_until_complete(plugin.authenticate(credentials))
        assert result == Authentication(user_id, user_name)
        store_credentials.assert_not_called()

    http_client.authenticate.assert_called_with(cookies)
    backend_client.get_identity.assert_called_with()


def test_updated_cookies(plugin, http_client, backend_client):
    loop = asyncio.get_event_loop()

    user_id = "13"
    persona_id = "19"
    user_name = "Jan"

    cookies = {
        "cookie": "value"
    }
    credentials = {
        "cookies": cookies
    }

    new_cookies = {
        "new_cookie": "new_value"
    }
    morsel = Morsel()
    morsel.set("new_cookie", "new_value", "new_value")
    new_credentials = {
        "cookies": new_cookies
    }

    http_client.authenticate.return_value = None

    def get_identity():
        callback = http_client.set_cookies_updated_callback.call_args[0][0]
        callback([morsel])
        return user_id, persona_id, user_name

    backend_client.get_identity.side_effect = get_identity

    with patch.object(plugin, "store_credentials") as store_credentials:
        result = loop.run_until_complete(plugin.authenticate(credentials))
        assert result == Authentication(user_id, user_name)
        store_credentials.assert_called_with(new_credentials)

    http_client.authenticate.assert_called_with(cookies)
    backend_client.get_identity.assert_called_with()


def test_auth_lost(authenticated_plugin, http_client):
    callback = http_client.set_auth_lost_callback.call_args[0][0]
    with patch.object(authenticated_plugin, "lost_authentication") as lost_authentication:
        callback()
        lost_authentication.assert_called_with()
