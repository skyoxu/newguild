extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _original_ci: String = ""
var _original_secure_mode: String = ""
var _original_backend: String = ""
var _original_allow_plugin: String = ""

func _new_db(name: String) -> Node:
	if not ClassDB.class_exists("SqliteDataStore"):
		push_warning("SqliteDataStore C# class not available; skipping DB backend policy hard tests.")
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
	_original_secure_mode = OS.get_environment("GD_SECURE_MODE")
	_original_backend = OS.get_environment("GODOT_DB_BACKEND")
	_original_allow_plugin = OS.get_environment("GD_DB_ALLOW_PLUGIN_BACKEND")

	OS.set_environment("CI", "1")
	OS.set_environment("GD_SECURE_MODE", "0")
	OS.set_environment("GODOT_DB_BACKEND", "plugin")
	OS.set_environment("GD_DB_ALLOW_PLUGIN_BACKEND", "1")


func after_test() -> void:
	if _original_ci != "":
		OS.set_environment("CI", _original_ci)
	else:
		OS.set_environment("CI", "")

	if _original_secure_mode != "":
		OS.set_environment("GD_SECURE_MODE", _original_secure_mode)
	else:
		OS.set_environment("GD_SECURE_MODE", "")

	if _original_backend != "":
		OS.set_environment("GODOT_DB_BACKEND", _original_backend)
	else:
		OS.set_environment("GODOT_DB_BACKEND", "")

	if _original_allow_plugin != "":
		OS.set_environment("GD_DB_ALLOW_PLUGIN_BACKEND", _original_allow_plugin)
	else:
		OS.set_environment("GD_DB_ALLOW_PLUGIN_BACKEND", "")


func test_plugin_backend_is_denied_in_ci_even_when_explicitly_allowed() -> void:
	_remove_audit_file()

	var db = await _new_db("DbBackendPolicy")
	if db == null:
		return

	var ok: bool = db.TryOpen("user://security_backend_policy.db")
	assert_bool(ok).is_false()

	await get_tree().process_frame

	var p: String = _audit_path()
	assert_bool(_audit_contains_action(p, "db.sqlite.plugin_backend_denied")).is_true()

