extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _store: Node
var _db: Node
var _types: Array[String] = []
var _db_path: String = ""

func before() -> void:
    OS.set_environment("CI", "1")
    OS.set_environment("GD_SECURE_MODE", "1")
    OS.set_environment("GD_DATASTORE_BACKEND", "sqlite")
    var ts := str(Time.get_unix_time_from_system())
    _db_path = "user://utdb_%s/ui_save_load.db" % ts
    OS.set_environment("GD_DATASTORE_DB_PATH", _db_path)

    _bus = _ensure_event_bus()
    _db = _ensure_sql_db(_db_path)
    _store = _ensure_data_store()
    _connect_bus()

func after() -> void:
    var ds = get_node_or_null("/root/DataStore")
    if ds != null and ds.has_method("DeleteSync"):
        ds.DeleteSync("ui_save_entry")

    if _db != null and _db.has_method("Close"):
        _db.Close()

    _delete_db_file(_db_path)

func _on_evt(type, _source, _data_json, _id, _spec, _ct, _ts) -> void:
    _types.append(str(type))

func _connect_bus() -> void:
    _types.clear()
    var cb := Callable(self, "_on_evt")
    if not _bus.is_connected("DomainEventEmitted", cb):
        _bus.connect("DomainEventEmitted", cb)

func _ensure_event_bus() -> Node:
    var existing = get_node_or_null("/root/EventBus")
    if existing != null:
        return existing
    var bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    bus.name = "EventBus"
    get_tree().get_root().add_child(auto_free(bus))
    return bus

func _ensure_sql_db(path: String) -> Node:
    var existing = get_node_or_null("/root/SqlDb")
    if existing != null:
        existing.free()
    var db = preload("res://Game.Godot/Adapters/SqliteDataStore.cs").new()
    db.name = "SqlDb"
    get_tree().get_root().add_child(auto_free(db))
    if db.has_method("TryOpen"):
        var ok = db.TryOpen(path)
        assert_bool(ok).is_true()
    return db

func _ensure_data_store() -> Node:
    var existing = get_node_or_null("/root/DataStore")
    if existing != null:
        return existing
    var store = preload("res://Game.Godot/Adapters/DataStoreAdapter.cs").new()
    store.name = "DataStore"
    get_tree().get_root().add_child(auto_free(store))
    return store

func _delete_db_file(path: String) -> void:
    if path == "":
        return
    var abs = ProjectSettings.globalize_path(path)
    if FileAccess.file_exists(path):
        DirAccess.remove_absolute(abs)
    var wal = abs + "-wal"
    var shm = abs + "-shm"
    if FileAccess.file_exists(wal):
        DirAccess.remove_absolute(wal)
    if FileAccess.file_exists(shm):
        DirAccess.remove_absolute(shm)

func _open_start_screen() -> Node:
    var screen = preload("res://Game.Godot/Scenes/Screens/StartScreen.tscn").instantiate()
    add_child(auto_free(screen))
    await get_tree().process_frame
    return screen

func test_start_screen_save_load_entry_reports_ok_and_emits_events() -> void:
    var screen = await _open_start_screen()
    var btn: Button = screen.get_node("Center/VBox/BtnSaveLoad")
    var out: Label = screen.get_node("Center/VBox/Output")

    _types.clear()
    btn.emit_signal("pressed")
    await get_tree().process_frame
    await get_tree().process_frame

    assert_str(out.text).contains("Save+Load")
    assert_str(out.text).contains("OK")
    assert_int(_types.find("core.save.requested")).is_greater_equal(0)
    assert_int(_types.find("core.save.completed")).is_greater_equal(0)
    assert_int(_types.find("core.load.requested")).is_greater_equal(0)
    assert_int(_types.find("core.load.completed")).is_greater_equal(0)
    assert_int(_types.find("core.save.failed")).is_equal(-1)
    assert_int(_types.find("core.load.failed")).is_equal(-1)
