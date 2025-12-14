extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"
## GdUnit4 audit log compliance test suite
## Tests security audit log format compliance per ADR-0019 and ADR-0004
##
## Test Coverage:
## - JSONL format validation: Each line is valid JSON
## - Required fields verification: All entries contain {ts, action, reason, target, caller}
## - Event naming convention: Action fields follow ADR-0004 format (domain.entity.verb)

const SecurityUrlAdapterFactory = preload("res://Game.Godot/Adapters/Security/security_url_adapter_factory.gd")
const SecurityFileAdapterFactory = preload("res://Game.Godot/Adapters/Security/security_file_adapter_factory.gd")
const SecurityProcessAdapterFactory = preload("res://Game.Godot/Adapters/Security/security_process_adapter_factory.gd")
const EventBusAdapter = preload("res://Game.Godot/Adapters/Security/EventBusAdapter.cs")

var _test_audit_log: String = ""
var _url_adapter = null
var _file_adapter = null
var _process_adapter = null
var _bus_adapter = null

func before_test() -> void:
	# Create unique audit log for testing (Windows path for C# System.IO compatibility)
	var timestamp = Time.get_unix_time_from_system()
	_test_audit_log = "logs/ci/test/security-audit-test-%d.jsonl" % timestamp

	# Initialize event bus
	_bus_adapter = EventBusAdapter.new()

	# Initialize all three security adapters
	var allowed_hosts = ["trusted.example.com"]
	_url_adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, _test_audit_log)
	_file_adapter = SecurityFileAdapterFactory.create_with_audit(_bus_adapter, _test_audit_log)

	# Reset environment for process adapter
	OS.set_environment("GD_SECURE_MODE", "0")
	OS.set_environment("SECURITY_TEST_MODE", "0")
	_process_adapter = SecurityProcessAdapterFactory.create_with_defaults(_bus_adapter, _test_audit_log)

func after_test() -> void:
	# Cleanup audit log
	if FileAccess.file_exists(_test_audit_log):
		DirAccess.remove_absolute(ProjectSettings.globalize_path("res://" + _test_audit_log))

	_url_adapter = null
	_file_adapter = null
	_process_adapter = null
	_bus_adapter = null

## Test 1: JSONL format validation - each line must be valid JSON
func test_audit_log_valid_jsonl_format() -> void:
	# GIVEN: Multiple audit entries from different adapters
	var _url_reject = _url_adapter.ValidateAndAudit("http://malicious.com", "test_jsonl")
	var _file_reject = _file_adapter.ValidateAndAudit("/etc/passwd", 1, "test_jsonl")
	var _process_reject = _process_adapter.ValidateAndAudit("rm", ["-rf", "/"], "test_jsonl")

	await get_tree().create_timer(0.15).timeout

	# WHEN: Reading audit log
	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	assert_object(file).is_not_null()

	var json = JSON.new()
	var all_valid_json = true
	var line_count = 0

	# THEN: Every non-empty line must be valid JSON
	while not file.eof_reached():
		var line = file.get_line()
		if line.strip_edges().is_empty():
			continue

		line_count += 1
		var parse_result = json.parse(line)
		if parse_result != OK:
			all_valid_json = false
			break

	file.close()

	assert_bool(all_valid_json).is_true()
	assert_int(line_count).is_greater_equal(3)  # At least 3 rejections

## Test 2: Required fields verification - all entries must have {ts, action, reason, target, caller}
func test_audit_log_has_required_fields() -> void:
	# GIVEN: Multiple audit entries from different adapters
	var _url_reject = _url_adapter.ValidateAndAudit("javascript:alert(1)", "test_fields")
	var _file_reject = _file_adapter.ValidateAndAudit("../../etc/shadow", 1, "test_fields")
	var _process_reject = _process_adapter.ValidateAndAudit("curl", ["http://evil.com"], "test_fields")

	await get_tree().create_timer(0.15).timeout

	# WHEN: Reading and parsing audit log
	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	assert_object(file).is_not_null()

	var json = JSON.new()
	var all_have_required_fields = true
	var entries_checked = 0

	# THEN: Every entry must have all required fields
	while not file.eof_reached():
		var line = file.get_line()
		if line.strip_edges().is_empty():
			continue

		var parse_result = json.parse(line)
		if parse_result != OK:
			all_have_required_fields = false
			break

		var entry = json.get_data()
		entries_checked += 1

		# Verify all required fields exist
		if not (entry.has("ts") and entry.has("action") and entry.has("reason") and entry.has("target") and entry.has("caller")):
			all_have_required_fields = false
			break

		# Verify fields are non-empty strings
		if not (entry["ts"] is String and entry["ts"].length() > 0):
			all_have_required_fields = false
			break
		if not (entry["action"] is String and entry["action"].length() > 0):
			all_have_required_fields = false
			break
		if not (entry["reason"] is String and entry["reason"].length() > 0):
			all_have_required_fields = false
			break
		if not (entry["target"] is String and entry["target"].length() > 0):
			all_have_required_fields = false
			break
		if not (entry["caller"] is String and entry["caller"].length() > 0):
			all_have_required_fields = false
			break

	file.close()

	assert_bool(all_have_required_fields).is_true()
	assert_int(entries_checked).is_greater_equal(3)  # At least 3 entries checked

## Test 3: Event naming convention - action fields must follow ADR-0004 format (domain.entity.verb)
func test_audit_log_follows_adr_naming_convention() -> void:
	# GIVEN: Multiple audit entries from different adapters
	var _url_reject = _url_adapter.ValidateAndAudit("ftp://unsafe.com", "test_naming")
	var _file_reject = _file_adapter.ValidateAndAudit("/tmp/evil.sh", 1, "test_naming")
	var _process_reject = _process_adapter.ValidateAndAudit("wget", ["http://malware.com"], "test_naming")

	await get_tree().create_timer(0.15).timeout

	# WHEN: Reading and parsing audit log
	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	assert_object(file).is_not_null()

	var json = JSON.new()
	var all_follow_naming_convention = true
	var entries_checked = 0

	# THEN: Every action field must follow domain.entity.verb pattern
	while not file.eof_reached():
		var line = file.get_line()
		if line.strip_edges().is_empty():
			continue

		var parse_result = json.parse(line)
		if parse_result != OK:
			all_follow_naming_convention = false
			break

		var entry = json.get_data()
		entries_checked += 1

		# Verify action field exists
		if not entry.has("action"):
			all_follow_naming_convention = false
			break

		var action = entry["action"]

		# ADR-0004: Security audit events must use security.{entity}.{verb} format
		# Valid patterns: security.url.rejected, security.file.rejected, security.process.rejected
		if not (action.begins_with("security.url.") or action.begins_with("security.file.") or action.begins_with("security.process.")):
			all_follow_naming_convention = false
			break

		# Verify action has at least 3 parts (domain.entity.verb)
		var parts = action.split(".")
		if parts.size() < 3:
			all_follow_naming_convention = false
			break

	file.close()

	assert_bool(all_follow_naming_convention).is_true()
	assert_int(entries_checked).is_greater_equal(3)  # At least 3 entries checked
