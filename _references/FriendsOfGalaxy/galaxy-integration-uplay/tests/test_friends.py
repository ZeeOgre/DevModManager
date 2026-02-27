import asyncio
import json
import pytest
from galaxy.api.types import FriendInfo

@pytest.fixture
def result_friends():
    data = json.loads('''{"jsonrpc": "2.0", "id": "1", "result": {"friend_info_list": [{"user_id": "420", "user_name": "mocker1"}, {"user_id": "123", "user_name": "mocker2"}, {"user_id": "321", "user_name": "mocker3"}]}}''')
    return [
        FriendInfo(user_id=friend["user_id"], user_name=friend["user_name"])
        for friend in data["result"]["friend_info_list"]
    ]


def test_friends(create_authenticated_plugin, result_friends):
    loop = asyncio.get_event_loop()
    pg = create_authenticated_plugin()

    result = loop.run_until_complete(pg.get_friends())

    assert result == result_friends