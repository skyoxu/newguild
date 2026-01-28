extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _sort_by_ts(a, b) -> bool:
    return int(a.get("ts", 0)) < int(b.get("ts", 0))

func _make_event(id: String, kind: String, ts: int) -> Dictionary:
    return {"id": id, "kind": kind, "ts": ts}

# ACC:T33.1
# ACC:T33.2
func test_timeline_sorts_by_timestamp() -> void:
    var events = [
        _make_event("e1", "raid", 300),
        _make_event("e2", "media", 100),
        _make_event("e3", "social", 200),
    ]
    events.sort_custom(Callable(self, "_sort_by_ts"))
    assert_bool(events[0]["ts"] == 100).is_true()
    assert_bool(events[2]["ts"] == 300).is_true()

# ACC:T33.3
func test_timeline_contains_expected_categories() -> void:
    var events = [
        _make_event("e1", "raid", 1),
        _make_event("e2", "media", 2),
        _make_event("e3", "social", 3),
    ]
    var kinds = []
    for event_item in events:
        kinds.append(event_item["kind"])
    assert_bool(kinds.size() == 3).is_true()
    assert_bool(kinds.has("raid")).is_true()
    assert_bool(kinds.has("media")).is_true()
    assert_bool(kinds.has("social")).is_true()

# ACC:T33.6
func test_timeline_event_fields_are_present() -> void:
    var evt = _make_event("e1", "raid", 10)
    assert_bool(evt.has("id")).is_true()
    assert_bool(evt.has("kind")).is_true()
    assert_bool(evt.has("ts")).is_true()
    assert_bool(typeof(evt["id"]) == TYPE_STRING).is_true()
    assert_bool(typeof(evt["kind"]) == TYPE_STRING).is_true()
    assert_bool(typeof(evt["ts"]) == TYPE_INT).is_true()
