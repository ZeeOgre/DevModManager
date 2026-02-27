import pytest
import os
# from local import LocalParser


@pytest.fixture
def good_yaml():
    with open(os.path.join('tests', 'resources', 'good.yaml'), 'rb') as r:
        return r.read()


# TODO mock registry
# def test_yaml_decent_parse(good_yaml):
#     count = 0
#     p = LocalParser(good_yaml)
#     p.parse_configuration()
#     for game in p.parse_games():
#         count += 1
#     assert count == 36
