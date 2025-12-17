extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"
## GdUnit4 integration test suite for Security Adapters cooperation
## Tests multi-adapter scenarios and security policy consistency per ADR-0019
##
## Test Coverage:
## - Multi-adapter cooperation: URL + File + Process adapters work together
## - Security policy consistency: Rejections across adapters follow same rules
## - Performance validation: Security checks complete within 100ms threshold
## - Cross-adapter audit: Multiple adapters log to same audit file correctly
## - Error isolation: Failure in one adapter doesn't affect others

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
	# Create unique shared audit log for all adapters
	var timestamp = Time.get_unix_time_from_system()
	_test_audit_log = "user://test-integration-audit-%d.jsonl" % timestamp

	# Initialize shared event bus
	_bus_adapter = EventBusAdapter.new()

	# Initialize all three security adapters sharing the same audit log
	var allowed_hosts = ["trusted.example.com"]
	_url_adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, _test_audit_log)
	_file_adapter = SecurityFileAdapterFactory.create_with_audit(_bus_adapter, _test_audit_log)

	# Reset environment for process adapter
	OS.set_environment("GD_SECURE_MODE", "0")
	OS.set_environment("SECURITY_TEST_MODE", "0")
	_process_adapter = SecurityProcessAdapterFactory.create_with_defaults(_bus_adapter, _test_audit_log)

	assert_object(_url_adapter).is_not_null()
	assert_object(_file_adapter).is_not_null()
	assert_object(_process_adapter).is_not_null()

func after_test() -> void:
	# Cleanup shared audit log
	if FileAccess.file_exists(_test_audit_log):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(_test_audit_log))

	_url_adapter = null
	_file_adapter = null
	_process_adapter = null
	_bus_adapter = null

## Test 1: Multi-adapter cooperation - all three adapters reject malicious inputs
func test_multi_adapter_rejects_coordinated_attack() -> void:
	# GIVEN: Malicious inputs targeting all three security layers
	var malicious_url = "http://evil.com/backdoor"  # Non-HTTPS
	var malicious_path = "../../etc/passwd"  # Path traversal
	var malicious_command = "rm"  # Not in whitelist
	var malicious_args = ["-rf", "/"]

	# WHEN: Validating across all adapters
	var url_result = _url_adapter.ValidateAndAudit(malicious_url, "test_multi_adapter")
	var file_result = _file_adapter.ValidateAndAudit(malicious_path, 1, "test_multi_adapter")  # READ_MODE = 1
	var process_result = _process_adapter.ValidateAndAudit(malicious_command, malicious_args, "test_multi_adapter")

	# THEN: All three adapters should reject
	assert_bool(url_result.Get(0)).is_false()
	assert_bool(file_result.IsAllowed).is_false()
	assert_bool(process_result.IsAllowed).is_false()

	# AND: All rejections should be logged to shared audit file
	await get_tree().create_timer(0.15).timeout  # Wait for all writes
	assert_bool(FileAccess.file_exists(_test_audit_log)).is_true()

## Test 2: Security policy consistency - all adapters use same audit format
func test_security_policy_consistency_across_adapters() -> void:
	# GIVEN: Invalid inputs for each adapter type
	var _url_reject = _url_adapter.ValidateAndAudit("javascript:alert(1)", "test_consistency")
	var _file_reject = _file_adapter.ValidateAndAudit("/etc/shadow", 1, "test_consistency")
	var _process_reject = _process_adapter.ValidateAndAudit("curl", ["http://evil.com"], "test_consistency")

	await get_tree().create_timer(0.15).timeout

	# WHEN: Reading audit log
	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	assert_object(file).is_not_null()

	var json = JSON.new()
	var line_count = 0
	var all_have_required_fields = true

	while not file.eof_reached():
		var line = file.get_line()
		if line.strip_edges().is_empty():
			continue

		var parse_result = json.parse(line)
		if parse_result != OK:
			all_have_required_fields = false
			break

		var entry = json.get_data()

		# THEN: All entries must have required fields
		if not (entry.has("ts") and entry.has("action") and entry.has("reason") and entry.has("target") and entry.has("caller")):
			all_have_required_fields = false
			break

		# AND: Action must follow ADR-0004 naming convention (domain.entity.verb)
		var action = entry["action"]
		if not (action.begins_with("security.url.") or action.begins_with("security.file.") or action.begins_with("security.process.")):
			all_have_required_fields = false
			break

		line_count += 1

	file.close()

	assert_bool(all_have_required_fields).is_true()
	assert_int(line_count).is_equal(3)  # One rejection per adapter

## Test 3: Performance validation - security checks complete within threshold
func test_security_checks_complete_within_performance_threshold() -> void:
	# GIVEN: Performance threshold of 100ms (ADR-0015)
	var threshold_ms = 100.0
	var total_time_ms = 0.0

	# WHEN: Running security checks across all adapters
	var start_time = Time.get_ticks_usec()

	# URL validation
	var _url_result = _url_adapter.ValidateAndAudit("https://trusted.example.com/api", "test_performance")

	# File validation
	var _file_result = _file_adapter.ValidateAndAudit("user://saves/game.dat", 1, "test_performance")

	# Process validation
	var _process_result = _process_adapter.ValidateAndAudit("git", ["status"], "test_performance")

	var end_time = Time.get_ticks_usec()
	total_time_ms = (end_time - start_time) / 1000.0

	# THEN: Combined validation time should be under threshold
	assert_float(total_time_ms).is_less(threshold_ms)

## Test 4: Cross-adapter audit - multiple adapters can log to same file concurrently
func test_cross_adapter_audit_log_integrity() -> void:
	# GIVEN: Multiple rapid operations across adapters
	for i in range(5):
		var _url_op = _url_adapter.ValidateAndAudit("http://unsafe-%d.com" % i, "test_audit_integrity")
		var _file_op = _file_adapter.ValidateAndAudit("/tmp/unsafe-%d" % i, 1, "test_audit_integrity")
		var _proc_op = _process_adapter.ValidateAndAudit("unsafe-cmd-%d" % i, [], "test_audit_integrity")

	await get_tree().create_timer(0.2).timeout

	# WHEN: Reading audit log
	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	var valid_json_lines = 0
	var json = JSON.new()

	while not file.eof_reached():
		var line = file.get_line()
		if line.strip_edges().is_empty():
			continue

		if json.parse(line) == OK:
			valid_json_lines += 1

	file.close()

	# THEN: Should have 15 valid JSONL entries (3 adapters * 5 operations)
	assert_int(valid_json_lines).is_equal(15)

## Test 5: Error isolation - failure in one adapter doesn't affect others
func test_error_isolation_between_adapters() -> void:
	# GIVEN: File adapter with invalid audit log path (must fail on Windows filesystem rules)
	# Use invalid characters to force System.IO to fail consistently (Windows-only project).
	var invalid_log_path = "user://logs/invalid<>/audit.jsonl"
	var failing_file_adapter = SecurityFileAdapterFactory.create_with_audit(_bus_adapter, invalid_log_path)

	# WHEN: File adapter triggers a denial and its audit write fails, but URL and Process adapters still function.
	var file_denied = failing_file_adapter.ValidateAndAudit("user://../test.dat", 1, "test_isolation")

	# Trigger audit writes on URL/Process by causing rejections.
	var url_reject = _url_adapter.ValidateAndAudit("http://trusted.example.com/api", "test_isolation")
	var process_reject = _process_adapter.ValidateAndAudit("unsafe-cmd", ["--token", "secret"], "test_isolation")

	# THEN: All three adapters should return valid results (no crash), and URL/Process should correctly reject.
	assert_bool(file_denied.IsAllowed).is_false()
	assert_bool(url_reject.Get(0)).is_false()
	assert_bool(process_reject.IsAllowed).is_false()

	# AND: URL/Process audit logs should still be written to the valid shared audit path
	await get_tree().create_timer(0.1).timeout
	assert_bool(FileAccess.file_exists(_test_audit_log)).is_true()
