extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _original_ci: String = ""
var _original_max_bytes: String = ""
var _test_db_path: String = ""

func _new_db(name: String) -> Node:
	if not ClassDB.class_exists("SqliteDataStore"):
		push_warning("SqliteDataStore C# class not available; skipping DB size limit hard tests.")
		return null

	var db: Node = ClassDB.instantiate("SqliteDataStore")
	db.name = name
	get_tree().get_root().add_child(auto_free(db))
	await get_tree().process_frame
	if not db.has_method("TryOpen"):
		await get_tree().process_frame
	return db


func _today_dir() -> String:
	var d = Time.get_datetime_dict_from_system()
	return "%04d-%02d-%02d" % [d.year, d.month, d.day]


func _audit_path() -> String:
	return "res://logs/ci/%s/security-audit.jsonl" % _today_dir()


func _remove_audit_file() -> void:
	var p: String = _audit_path()
	if FileAccess.file_exists(p):
		var abs: String = ProjectSettings.globalize_path(p)
		DirAccess.remove_absolute(abs)


func _audit_contains_action(audit_path: String, expected_action: String) -> bool:
	if not FileAccess.file_exists(audit_path):
		return false

	var txt: String = FileAccess.get_file_as_string(audit_path)
	if txt == "":
		return false

	var expected = expected_action.to_lower()
	for raw in txt.split("\n", false):
		var line = raw.strip_edges()
		if line == "":
			continue
		var parsed = JSON.parse_string(line)
		if parsed == null:
			continue
		var action = str(parsed.get("action", "")).to_lower()
		if action == expected:
			return true
	return false


func before_test() -> void:
	_original_ci = OS.get_environment("CI")
	_original_max_bytes = OS.get_environment("GD_DB_MAX_BYTES")

	OS.set_environment("CI", "1")
	OS.set_environment("GD_DB_MAX_BYTES", "128")

	var ts = Time.get_unix_time_from_system()
	_test_db_path = "user://security_size_limit_%d.db" % ts


func after_test() -> void:
	# Restore environment variables (empty string is treated as unset by C# IsNullOrWhiteSpace checks)
	if _original_ci != "":
		OS.set_environment("CI", _original_ci)
	else:
		OS.set_environment("CI", "")
	if _original_max_bytes != "":
		OS.set_environment("GD_DB_MAX_BYTES", _original_max_bytes)
	else:
		OS.set_environment("GD_DB_MAX_BYTES", "")

	# Cleanup test DB file
	if _test_db_path != "" and FileAccess.file_exists(_test_db_path):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(_test_db_path))


func test_try_open_existing_db_over_size_limit_is_rejected_and_audited() -> void:
	_remove_audit_file()

	# Create an existing file that exceeds GD_DB_MAX_BYTES
	var f = FileAccess.open(_test_db_path, FileAccess.WRITE)
	assert_object(f).is_not_null()
	f.store_string("A".repeat(1024))
	f.close()

	var db = await _new_db("DbSizeLimit")
	if db == null:
		return

	var ok: bool = db.TryOpen(_test_db_path)
	assert_bool(ok).is_false()

	await get_tree().process_frame

	var p: String = _audit_path()
	assert_bool(_audit_contains_action(p, "db.sqlite.size_limit_exceeded")).is_true()

