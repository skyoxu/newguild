extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _store: Node
var _db: Node
var _types: Array[String] = []
var _snapshot_audits: Array = []
var _db_path: String = ""

class FakeDataStore:
	extends Node

	var _data := {}

	func TrySaveSync(key: String, json: String) -> bool:
		if key == "ui_save_entry_xp_state" and json == "":
			return false
		_data[key] = json
		return true

	func TryLoadSync(key: String):
		if _data.has(key):
			return _data[key]
		return null


class FakeGuildManager:
	extends Node

	var _summary_json := "{}"

	func HasCurrentGuild() -> bool:
		return true

	func GetCurrentGuildSummaryJson() -> String:
		return _summary_json

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
    if str(type) == "core.security.snapshot.decision":
        var parsed = JSON.parse_string(str(_data_json))
        if typeof(parsed) == TYPE_DICTIONARY:
            _snapshot_audits.append(parsed)

func _connect_bus() -> void:
    _types.clear()
    _snapshot_audits.clear()
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


func test_start_screen_save_load_fails_when_invalid_snapshot_cannot_be_cleared() -> void:
	var existing_store = get_node_or_null("/root/DataStore")
	if existing_store != null:
		existing_store.queue_free()
		await get_tree().process_frame

	var fake_store := FakeDataStore.new()
	fake_store.name = "DataStore"
	fake_store._data["ui_save_entry_xp_state"] = "{\"guildId\":\"guild-1\",\"totalExperience\":10,\"level\":1,\"changedAt\":\"invalid\"}"
	get_tree().get_root().add_child(auto_free(fake_store))
	await get_tree().process_frame

	var screen = await _open_start_screen()
	var btn: Button = screen.get_node("Center/VBox/BtnSaveLoad")
	var out: Label = screen.get_node("Center/VBox/Output")

	_types.clear()
	btn.emit_signal("pressed")
	await get_tree().process_frame
	await get_tree().process_frame

	assert_str(out.text).contains("FAILED")
	assert_int(_types.find("core.save.failed")).is_greater_equal(0)
	assert_int(_types.find("core.load.failed")).is_greater_equal(0)


func test_start_screen_rejects_untrusted_experience_source() -> void:
	var _screen = await _open_start_screen()
	_types.clear()
	_snapshot_audits.clear()

	var payload := "{\"guildId\":\"npc-guild-01\",\"totalExperience\":120,\"delta\":10,\"level\":2,\"sourceEventType\":\"core.raid.resolved\",\"changedAt\":\"2025-01-01T00:00:00Z\"}"
	_bus.PublishSimple("core.experience.changed", "evil", payload)
	await get_tree().process_frame
	await get_tree().process_frame

	assert_int(_types.find("core.security.snapshot.decision")).is_greater_equal(0)
	var found := false
	for item in _snapshot_audits:
		if str(item.get("action", "")) == "invalid" and str(item.get("reason", "")) == "untrusted_source":
			found = true
			break
	assert_bool(found).is_true()


func test_start_screen_rejects_untrusted_snapshot_source_event_type() -> void:
	var existing_store = get_node_or_null("/root/DataStore")
	if existing_store != null:
		existing_store.queue_free()
		await get_tree().process_frame

	var fake_store := FakeDataStore.new()
	fake_store.name = "DataStore"
	fake_store._data["ui_save_entry_xp_state"] = "{\"guildId\":\"npc-guild-01\",\"totalExperience\":120,\"delta\":10,\"level\":2,\"sourceEventType\":\"core.untrusted.source\",\"changedAt\":\"2025-01-01T00:00:00Z\"}"
	get_tree().get_root().add_child(auto_free(fake_store))
	await get_tree().process_frame

	var existing_guild_manager = get_node_or_null("/root/GuildManager")
	if existing_guild_manager != null:
		existing_guild_manager.queue_free()
		await get_tree().process_frame

	var fake_guild_manager := FakeGuildManager.new()
	fake_guild_manager.name = "GuildManager"
	fake_guild_manager._summary_json = "{\"guildId\":\"npc-guild-01\",\"creatorId\":\"u1\",\"guildName\":\"Demo\",\"createdAt\":\"2025-01-01T00:00:00Z\"}"
	get_tree().get_root().add_child(auto_free(fake_guild_manager))
	await get_tree().process_frame

	var screen = await _open_start_screen()
	var btn: Button = screen.get_node("Center/VBox/BtnSaveLoad")
	_types.clear()
	_snapshot_audits.clear()

	btn.emit_signal("pressed")
	await get_tree().process_frame
	await get_tree().process_frame

	assert_int(_types.find("core.security.snapshot.decision")).is_greater_equal(0)
	var found := false
	for item in _snapshot_audits:
		if str(item.get("action", "")) == "invalid" and str(item.get("reason", "")) == "untrusted_source":
			found = true
			break
	assert_bool(found).is_true()


func test_start_screen_rejects_guild_mismatch_experience_source() -> void:
	var existing_store = get_node_or_null("/root/DataStore")
	if existing_store != null:
		existing_store.queue_free()
		await get_tree().process_frame

	var fake_store := FakeDataStore.new()
	fake_store.name = "DataStore"
	fake_store._data["ui_save_entry_xp_state"] = "{\"guildId\":\"other-guild\",\"totalExperience\":120,\"delta\":10,\"level\":2,\"sourceEventType\":\"core.raid.resolved\",\"changedAt\":\"2025-01-01T00:00:00Z\"}"
	get_tree().get_root().add_child(auto_free(fake_store))
	await get_tree().process_frame

	var existing_guild_manager = get_node_or_null("/root/GuildManager")
	if existing_guild_manager != null:
		existing_guild_manager.queue_free()
		await get_tree().process_frame

	var fake_guild_manager := FakeGuildManager.new()
	fake_guild_manager.name = "GuildManager"
	fake_guild_manager._summary_json = "{\"guildId\":\"npc-guild-01\",\"creatorId\":\"u1\",\"guildName\":\"Demo\",\"createdAt\":\"2025-01-01T00:00:00Z\"}"
	get_tree().get_root().add_child(auto_free(fake_guild_manager))
	await get_tree().process_frame

	var screen = await _open_start_screen()
	var btn: Button = screen.get_node("Center/VBox/BtnSaveLoad")
	_types.clear()
	_snapshot_audits.clear()

	btn.emit_signal("pressed")
	await get_tree().process_frame
	await get_tree().process_frame

	assert_int(_types.find("core.security.snapshot.decision")).is_greater_equal(0)
	var found := false
	for item in _snapshot_audits:
		if str(item.get("action", "")) == "invalid" and str(item.get("reason", "")) == "guild_mismatch":
			found = true
			break
	assert_bool(found).is_true()


func test_start_screen_rejects_missing_guild_context_by_default() -> void:
	var existing_store = get_node_or_null("/root/DataStore")
	if existing_store != null:
		existing_store.queue_free()
		await get_tree().process_frame

	var fake_store := FakeDataStore.new()
	fake_store.name = "DataStore"
	fake_store._data["ui_save_entry_xp_state"] = "{\"guildId\":\"npc-guild-01\",\"totalExperience\":120,\"delta\":10,\"level\":2,\"sourceEventType\":\"core.raid.resolved\",\"changedAt\":\"2025-01-01T00:00:00Z\"}"
	get_tree().get_root().add_child(auto_free(fake_store))
	await get_tree().process_frame

	var existing_guild_manager = get_node_or_null("/root/GuildManager")
	if existing_guild_manager != null:
		existing_guild_manager.queue_free()
		await get_tree().process_frame

	OS.set_environment("GD_SNAPSHOT_ALLOW_MISSING_GUILD_CONTEXT", "0")

	var screen = await _open_start_screen()
	var btn: Button = screen.get_node("Center/VBox/BtnSaveLoad")
	_types.clear()
	_snapshot_audits.clear()

	btn.emit_signal("pressed")
	await get_tree().process_frame
	await get_tree().process_frame

	assert_int(_types.find("core.security.snapshot.decision")).is_greater_equal(0)
	var found := false
	for item in _snapshot_audits:
		if str(item.get("action", "")) == "invalid" and str(item.get("reason", "")) == "guild_context_missing":
			found = true
			break
	assert_bool(found).is_true()
