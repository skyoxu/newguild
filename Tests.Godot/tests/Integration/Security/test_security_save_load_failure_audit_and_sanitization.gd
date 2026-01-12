extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const _AUDIT_FILE_NAME := "security-audit.jsonl"
const _KEEP_AUDIT_ENV := "KEEP_AUDIT"

var _prev_ci := ""
var _prev_secure := ""
var _prev_audit_root := ""
var _audit_root_abs := ""
var _bus: Node
var _bad_db_abs := ""

func before_test() -> void:
	_prev_ci = OS.get_environment("CI")
	_prev_secure = OS.get_environment("GD_SECURE_MODE")
	_prev_audit_root = OS.get_environment("AUDIT_LOG_ROOT")

	OS.set_environment("CI", "1")
	OS.set_environment("GD_SECURE_MODE", "1")

	var rel_root := "user://logs/ci/test-save-load-security"
	_audit_root_abs = ProjectSettings.globalize_path(rel_root)
	DirAccess.make_dir_recursive_absolute(_audit_root_abs)
	OS.set_environment("AUDIT_LOG_ROOT", _audit_root_abs)

	_bus = _ensure_event_bus()
	_remove_audit_file()

func after_test() -> void:
	var keep := (OS.get_environment(_KEEP_AUDIT_ENV) == "1")
	if not keep:
		_remove_audit_file()
	if _bad_db_abs != "" and DirAccess.dir_exists_absolute(_bad_db_abs):
		# Best-effort cleanup; directory should be empty.
		DirAccess.remove_absolute(_bad_db_abs)
	_bad_db_abs = ""
	OS.set_environment("CI", _prev_ci)
	OS.set_environment("GD_SECURE_MODE", _prev_secure)
	OS.set_environment("AUDIT_LOG_ROOT", _prev_audit_root)


func _ensure_event_bus() -> Node:
	var existing = get_node_or_null("/root/EventBus")
	if existing != null:
		return existing
	var bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	bus.name = "EventBus"
	get_tree().get_root().add_child(auto_free(bus))
	return bus

func _audit_file_abs() -> String:
	if _audit_root_abs == "":
		return ""
	return _audit_root_abs.path_join(_AUDIT_FILE_NAME)

func _remove_audit_file() -> void:
	var p := _audit_file_abs()
	if p != "" and FileAccess.file_exists(p):
		DirAccess.remove_absolute(p)

func _read_last_non_empty_line(abs_path: String) -> String:
	if abs_path == "" or not FileAccess.file_exists(abs_path):
		return ""
	var bytes := FileAccess.get_file_as_bytes(abs_path)
	if bytes.size() == 0:
		return ""
	var txt := bytes.get_string_from_utf8()
	if txt.contains("\u0000"):
		var filtered := PackedByteArray()
		for b in bytes:
			if int(b) != 0:
				filtered.append(int(b))
		txt = filtered.get_string_from_utf8()
	if txt == "":
		return ""
	txt = txt.replace("\r\n", "\n")
	var lines := txt.split("\n", false)
	for i in range(lines.size() - 1, -1, -1):
		var line := str(lines[i]).strip_edges()
		if line != "":
			return line
	return ""


func _parse_json_line(line: String):
	var s := str(line).strip_edges()
	# Tolerate UTF-8 BOM if present (some writers may create the file with BOM).
	if s.begins_with("\ufeff"):
		s = s.substr(1)
	# Tolerate accidental UTF-16/invalid decoding artifacts (NUL separators).
	if s.contains("\u0000"):
		s = s.replace("\u0000", "")
	# Best-effort: strip any non-JSON prefix/suffix.
	var first := s.find("{")
	var last := s.rfind("}")
	if first >= 0 and last > first:
		s = s.substr(first, last - first + 1)
	return JSON.parse_string(s)

func _assert_audit_line_has_required_fields_and_is_sanitized(line: String) -> void:
	var obj = _parse_json_line(line)
	assert_that(obj).is_not_null()
	if obj == null:
		return
	assert_bool(obj.has("ts")).is_true()
	assert_bool(obj.has("action")).is_true()
	assert_bool(obj.has("reason")).is_true()
	assert_bool(obj.has("target")).is_true()
	assert_bool(obj.has("caller")).is_true()

	var reason := str(obj.get("reason", ""))
	assert_str(reason).not_contains("C:\\")
	assert_str(reason).not_contains("SELECT")

func test_contract_files_exist_for_save_load_events() -> void:
	var save_failed_path := "res://Game.Core/Contracts/Persistence/SaveFailed.cs"
	var load_failed_path := "res://Game.Core/Contracts/Persistence/LoadFailed.cs"
	assert_bool(FileAccess.file_exists(save_failed_path)).is_true()
	assert_bool(FileAccess.file_exists(load_failed_path)).is_true()

	var save_failed_src := FileAccess.get_file_as_string(save_failed_path)
	var load_failed_src := FileAccess.get_file_as_string(load_failed_path)
	assert_str(save_failed_src).contains("public const string EventType = \"core.save.failed\";")
	assert_str(load_failed_src).contains("public const string EventType = \"core.load.failed\";")

# ACC:T26.2
func test_secure_mode_sanitizes_db_open_error_and_writes_audit_log() -> void:
	var sc = load("res://Game.Godot/Adapters/SqliteDataStore.cs")
	if sc == null or not sc.has_method("new"):
		push_warning("SKIP: CSharpScript.new() unavailable; skipping save/load failure audit scaffolding.")
		return

	var db = sc.new()
	add_child(auto_free(db))
	await get_tree().process_frame

	# Force an "open failed" condition deterministically by making the db path a directory.
	var bad_db_virtual := "user://bad.db"
	_bad_db_abs = ProjectSettings.globalize_path(bad_db_virtual)
	if not DirAccess.dir_exists_absolute(_bad_db_abs):
		DirAccess.make_dir_recursive_absolute(_bad_db_abs)

	var ok := bool(db.TryOpen(bad_db_virtual))
	assert_bool(ok).is_false()

	var msg := str(db.LastError).strip_edges().replace("\u0000", "")
	assert_str(msg).is_equal("Database open failed.")
	assert_str(msg).not_contains("user://")
	assert_str(msg).not_contains("C:\\")
	assert_str(msg).not_contains("SELECT")

	var audit_path := _audit_file_abs()
	assert_bool(FileAccess.file_exists(audit_path)).is_true()
	if not FileAccess.file_exists(audit_path):
		return

	var last := _read_last_non_empty_line(audit_path)
	assert_str(last).is_not_empty()
	if last == "":
		return
	_assert_audit_line_has_required_fields_and_is_sanitized(last)

	var obj = _parse_json_line(last)
	assert_that(obj).is_not_null()
	if obj == null:
		return
	assert_str(str(obj.get("action", ""))).is_equal("db.sqlite.open_failed")
	assert_str(str(obj.get("caller", ""))).contains("SqliteDataStore")
