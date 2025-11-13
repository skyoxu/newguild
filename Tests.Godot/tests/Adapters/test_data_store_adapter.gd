extends "res://addons/gdUnit4/src/core/GdUnitTestSuite.gd"

var _store: Node

func before() -> void:
    _store = load("res://Game.Godot/Adapters/DataStoreAdapter.cs").new()
    add_child_autofree(_store)

func test_save_load_delete_user_path() -> void:
    var key := "selfcheck/test-" + str(Time.get_unix_time_from_system())
    var payload := "{\"ok\":true}"
    _store.SaveSync(key, payload)
    var loaded := _store.LoadSync(key)
    assert_str(loaded).is_equal(payload)
    _store.DeleteSync(key)
    var after := _store.LoadSync(key)
    assert_that(after).is_null()

func test_make_safe_key_with_invalid_chars() -> void:
    var key := "a/b?c:*|<>"
    var payload := "X"
    _store.SaveSync(key, payload)
    var loaded := _store.LoadSync(key)
    assert_str(loaded).is_equal(payload)
    _store.DeleteSync(key)
