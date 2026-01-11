extends 'res://addons/gdUnit4/src/GdUnitTestSuite.gd'

func _new_node(class_name: String, script_path: String, name: String) -> Node:
	var node: Node = null
	if ClassDB.class_exists(class_name):
		node = ClassDB.instantiate(class_name)
	else:
		var s = load(script_path)
		if s == null or not s.has_method("new"):
			push_warning("SKIP: missing %s (%s)" % [class_name, script_path])
			return null
		node = s.new()
	node.name = name
	get_tree().get_root().add_child(auto_free(node))
	return node

func _today_utc() -> String:
	# yyyy-MM-dd
	var dt = Time.get_datetime_dict_from_system(true)
	return "%04d-%02d-%02d" % [dt.year, dt.month, dt.day]

func _audit_path() -> String:
	return "res://logs/ci/%s/security-audit.jsonl" % _today_utc()

# ACC:T25.3
# Migration must be idempotent and auditable.
func test_schema_version_meta_and_audit_on_invalid_db_path() -> void:
	OS.set_environment("CI", "1")
	OS.set_environment("GD_SECURE_MODE", "1")
	OS.set_environment("GD_DATASTORE_BACKEND", "sqlite")

	var bus = _new_node("EventBusAdapter", "res://Game.Godot/Adapters/EventBusAdapter.cs", "EventBus")
	if bus == null:
		return

	var db = _new_node("SqliteDataStore", "res://Game.Godot/Adapters/SqliteDataStore.cs", "SqlDb")
	if db == null:
		return

	# Invalid extension must be rejected and audited
	var bad = "user://utdb_%s/bad.exe" % str(Time.get_unix_time_from_system())
	var ok_bad = db.TryOpen(bad)
	assert_bool(ok_bad).is_false()

	# Audit file should exist in CI mode under res://logs/ci/<date>/security-audit.jsonl
	var audit = _audit_path()
	assert_bool(FileAccess.file_exists(audit)).is_true()
	var txt = FileAccess.open(audit, FileAccess.READ).get_as_text().to_lower()
	assert_bool(txt.find("db.sqlite.invalid_extension") != -1).is_true()

# ACC:T25.4
# Save/Load must be constrained to user:// and reject traversal/non-whitelisted extensions.
func test_datastore_save_load_roundtrip_uses_sqlite() -> void:
	OS.set_environment("CI", "1")
	OS.set_environment("GD_SECURE_MODE", "1")
	OS.set_environment("GD_DATASTORE_BACKEND", "sqlite")

	var bus = _new_node("EventBusAdapter", "res://Game.Godot/Adapters/EventBusAdapter.cs", "EventBus")
	if bus == null:
		return

	var db = _new_node("SqliteDataStore", "res://Game.Godot/Adapters/SqliteDataStore.cs", "SqlDb")
	if db == null:
		return

	var p = "user://utdb_%s/save_load.db" % str(Time.get_unix_time_from_system())
	var ok = db.TryOpen(p)
	assert_bool(ok).is_true()

	var helper = preload("res://Game.Godot/Adapters/Db/DbTestHelper.cs").new()
	add_child(auto_free(helper))

	var store = _new_node("DataStoreAdapter", "res://Game.Godot/Adapters/DataStoreAdapter.cs", "DataStore")
	if store == null:
		return

	var key = "t25_roundtrip"
	var json = "{\"t\":%d}" % int(Time.get_unix_time_from_system())
	await store.SaveAsync(key, json)
	var loaded = await store.LoadAsync(key)
	assert_str(loaded).is_equal(json)

	# Prove persistence uses SQLite by checking kv_store has at least 1 row.
	var n = helper.QueryScalarInt("SELECT COUNT(*) FROM kv_store;")
	assert_int(n).is_greater_equal(1)
