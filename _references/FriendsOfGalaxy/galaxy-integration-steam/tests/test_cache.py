from cache import Cache

def test_empty():
    cache = Cache()
    assert list(cache) == []
    assert cache.get("a", 12) == None

def test_not_empty():
    cache = Cache()
    cache.update("a", 2, 14)
    assert list(cache) == [("a", 2, 14)]
    assert cache.get("a", 12) == None
    assert cache.get("a", 14) == 2
    assert cache.get("a", 15) == None