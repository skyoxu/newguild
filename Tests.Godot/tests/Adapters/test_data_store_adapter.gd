extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _store: Node

func _new_db() -> Node:
    var db = null
    if ClassDB.class_exists("SqliteDataStore"):
        db = ClassDB.instantiate("SqliteDataStore")
    else:
        var s = load("res://Game.Godot/Adapters/SqliteDataStore.cs")
        if s == null or not s.has_method("new"):
            push_warning("SKIP: CSharpScript.new() unavailable, skip datastore adapter test")
            return null
        db = s.new()
    db.name = "SqlDb"
    get_tree().get_root().add_child(auto_free(db))
    return db

func before() -> void:
    OS.set_environment("GD_DATASTORE_BACKEND", "sqlite")

    var existing_db = get_node_or_null("/root/SqlDb")
    if existing_db != null:
        existing_db.free()

    var db = _new_db()
    if db == null:
        return

    var p = "user://utdb_%s/datastore.db" % str(Time.get_unix_time_from_system())
    OS.set_environment("GD_DATASTORE_DB_PATH", p)
    assert_bool(db.TryOpen(p)).is_true()

    _store = load("res://Game.Godot/Adapters/DataStoreAdapter.cs").new()
    add_child(auto_free(_store))
    await get_tree().process_frame

func test_save_load_delete_user_path() -> void:
    if _store == null:
        push_warning("SKIP: DataStoreAdapter not initialized")
        return
    var key := "selfcheck/test-" + str(Time.get_unix_time_from_system())
    var payload := "{\"ok\":true}"
    _store.SaveSync(key, payload)
    var loaded = _store.LoadSync(key)
    assert_str(loaded).is_equal(payload)
    _store.DeleteSync(key)
    var after_value = _store.LoadSync(key)
    # Godot C# interop: C# null string can appear as empty in GDScript
    assert_str(str(after_value)).is_empty()

func test_make_safe_key_with_invalid_chars() -> void:
    if _store == null:
        push_warning("SKIP: DataStoreAdapter not initialized")
        return
    var key := "a/b?c:*|<>"
    var payload := "X"
    _store.SaveSync(key, payload)
    var loaded = _store.LoadSync(key)
    assert_str(loaded).is_equal(payload)
    _store.DeleteSync(key)

