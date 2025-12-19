extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _audit_root_rel() -> String:
	return "logs/ci/test/db-sanitization"


func _audit_root_abs() -> String:
	return ProjectSettings.globalize_path("res://" + _audit_root_rel())


func _audit_path_res() -> String:
	return "res://" + _audit_root_rel() + "/security-audit.jsonl"


func _remove_audit_file() -> void:
	var p: String = _audit_path_res()
	if FileAccess.file_exists(p):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(p))


func _load_probe() -> Node:
	if not ClassDB.class_exists("GodotSQLiteDatabaseErrorProbe"):
		push_warning("GodotSQLiteDatabaseErrorProbe C# class not available; skipping sanitization integration tests.")
		return null
	var probe: Node = ClassDB.instantiate("GodotSQLiteDatabaseErrorProbe")
	get_tree().get_root().add_child(auto_free(probe))
	await get_tree().process_frame
	return probe


func _assert_audit_line_has_required_fields(line: String) -> void:
	var parsed = JSON.parse_string(line)
	assert_object(parsed).is_not_null()
	assert_bool(parsed.has("ts")).is_true()
	assert_bool(parsed.has("action")).is_true()
	assert_bool(parsed.has("reason")).is_true()
	assert_bool(parsed.has("target")).is_true()
	assert_bool(parsed.has("caller")).is_true()
	var reason = str(parsed.get("reason", ""))
	assert_str(reason).does_not_contain("C:\\")
	assert_str(reason).does_not_contain("SELECT")


func test_secure_mode_sanitizes_open_error_and_writes_audit_log() -> void:
	_remove_audit_file()

	var probe = await _load_probe()
	if probe == null:
		return

	var result = probe.RunOpenAsyncAndCapture(
		"user://bad:db.db",
		true,
		true,
		_audit_root_abs()
	)

	assert_bool(bool(result.get("threw", false))).is_true()
	assert_bool(bool(result.get("has_inner", true))).is_false()
	assert_str(str(result.get("message", ""))).is_equal("Database operation failed.")

	assert_bool(FileAccess.file_exists(_audit_path_res())).is_true()
	var txt: String = FileAccess.get_file_as_string(_audit_path_res())
	assert_str(txt).is_not_empty()

	var found := false
	for raw in txt.split("\n", false):
		var line := str(raw).strip_edges()
		if line == "":
			continue
		_assert_audit_line_has_required_fields(line)
		var parsed = JSON.parse_string(line)
		var action = str(parsed.get("action", ""))
		if action == "db.sqlite.open_failed":
			found = true
			break

	assert_bool(found).is_true()


func test_debug_mode_keeps_details_for_query_error_and_does_not_write_audit_log() -> void:
	_remove_audit_file()

	var probe = await _load_probe()
	if probe == null:
		return

	var sql := "SELECT * FROM NonExistingTable WHERE password = @P0"
	var result = probe.RunExecuteNonQueryAsyncAndCapture(
		"user://sanitization_ok.db",
		sql,
		false,
		false,
		_audit_root_abs()
	)

	assert_bool(bool(result.get("threw", false))).is_true()
	assert_bool(bool(result.get("has_inner", false))).is_true()

	var msg := str(result.get("message", ""))
	assert_str(msg).contains("db=user://sanitization_ok.db")
	assert_str(msg).contains("sql=")
	assert_str(msg).contains("SELECT * FROM NonExistingTable")
	assert_str(msg).contains("@P0")

	assert_bool(FileAccess.file_exists(_audit_path_res())).is_false()


func test_encoded_path_traversal_is_rejected() -> void:
	var probe = await _load_probe()
	if probe == null:
		return

	var result = probe.RunOpenAsyncAndCapture(
		"user://%2e%2e/evil.db",
		true,
		true,
		_audit_root_abs()
	)

	assert_bool(bool(result.get("threw", false))).is_true()
