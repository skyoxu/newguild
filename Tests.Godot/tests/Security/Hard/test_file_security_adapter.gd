extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"
## GdUnit4 test suite for SecurityFileAdapter
## Validates file path security enforcement with comprehensive scenarios:
## - ALLOW: Valid res:// read and user:// read/write operations
## - DENY: Path traversal, absolute paths, protocol violations, extension whitelist
## - INVALID: Null/empty paths, malformed inputs
## - AUDIT: JSONL logging of security rejections
##
## Implements Task 8.2 acceptance criteria: 15+ tests covering all security scenarios
## References: ADR-0019 (Security Baseline), ADR-0007 (Ports and Adapters)

const EventBusAdapter = preload("res://Game.Godot/Adapters/Security/EventBusAdapter.cs")
const SecurityFileAdapterFactory = preload("res://Game.Godot/Adapters/Security/security_file_adapter_factory.gd")

# Test file path for audit logging
var _test_audit_log: String = ""
var _adapter = null
var _bus_adapter = null

# FileAccessMode enum values (matches C# ISecurityFileValidator.FileAccessMode)
const READ_MODE = 0
const WRITE_MODE = 1

func before_test() -> void:
	# Create unique audit log path for each test
	var timestamp = Time.get_unix_time_from_system()
	_test_audit_log = "user://test-audit-%d.jsonl" % timestamp

	# Initialize event bus adapter and file adapter with audit logging
	_bus_adapter = EventBusAdapter.new()

	_adapter = SecurityFileAdapterFactory.create_with_audit(_bus_adapter, _test_audit_log)

	assert_object(_adapter).is_not_null()

func after_test() -> void:
	# Cleanup audit log file
	if FileAccess.file_exists(_test_audit_log):
		DirAccess.remove_absolute(_test_audit_log)

	_adapter = null
	_bus_adapter = null

# ============================================================================
# ALLOW Scenarios (3 tests)
# ============================================================================

func test_allows_res_read_valid_file() -> void:
	# GIVEN: Valid res:// path with allowed extension
	var path = "res://data/config.json"

	# WHEN: Validating read access
	var result = _adapter.IsPathAllowed(path, READ_MODE)

	# THEN: Access should be allowed
	assert_bool(result).is_true()

func test_allows_user_read_write_valid_file() -> void:
	# GIVEN: Valid user:// path with allowed extension
	var path = "user://saves/game.sav"

	# WHEN: Validating write access
	var result_write = _adapter.IsPathAllowed(path, WRITE_MODE)
	var result_read = _adapter.IsPathAllowed(path, READ_MODE)

	# THEN: Both read and write should be allowed
	assert_bool(result_write).is_true()
	assert_bool(result_read).is_true()

func test_allows_nested_user_directories() -> void:
	# GIVEN: Nested user:// directory path
	var path = "user://data/profiles/player1/settings.cfg"

	# WHEN: Validating read access
	var result = _adapter.IsPathAllowed(path, READ_MODE)

	# THEN: Access should be allowed
	assert_bool(result).is_true()

# ============================================================================
# DENY Scenarios (8 tests - exceeds minimum requirement of 7)
# ============================================================================

func test_rejects_absolute_path_windows() -> void:
	# GIVEN: Windows absolute path
	var path = "C:/Windows/System32/config.txt"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_absolute_path_windows")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("Absolute path rejected")

func test_rejects_absolute_path_unix() -> void:
	# GIVEN: Unix absolute path
	var path = "/etc/passwd"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_absolute_path_unix")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("Absolute path rejected")

func test_rejects_path_traversal_dotdot_slash() -> void:
	# GIVEN: Path with ../ traversal attempt
	var path = "user://../../../etc/passwd"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_path_traversal_dotdot_slash")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("traversal pattern")

func test_rejects_path_traversal_dotdot_backslash() -> void:
	# GIVEN: Path with ..\\ traversal attempt (Windows style)
	var path = "user://..\\..\\..\\Windows\\System32\\config.txt"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_path_traversal_dotdot_backslash")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("traversal pattern")

func test_rejects_path_traversal_url_encoded() -> void:
	# GIVEN: URL-encoded path traversal (%2e%2e = ..)
	var path = "user://%2e%2e/%2e%2e/etc/passwd"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_path_traversal_url_encoded")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("traversal pattern")

func test_rejects_disallowed_extension() -> void:
	# GIVEN: File with non-whitelisted extension (.exe)
	var path = "user://malware.exe"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_disallowed_extension")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("extension not allowed")

func test_rejects_res_write_attempt() -> void:
	# GIVEN: res:// path (read-only protocol)
	var path = "res://data/config.json"

	# WHEN: Attempting write access
	var result = _adapter.ValidateAndAudit(path, WRITE_MODE, "test_rejects_res_write_attempt")

	# THEN: Write access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("Write access denied for res://")

func test_rejects_invalid_protocol() -> void:
	# GIVEN: Path with invalid protocol (not res:// or user://)
	var path = "http://example.com/data.txt"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_invalid_protocol")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("Invalid protocol")

# ============================================================================
# INVALID Scenarios (2 tests)
# ============================================================================

func test_rejects_null_or_empty_path() -> void:
	# GIVEN: Empty path string
	var path = ""

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_null_or_empty_path")

	# THEN: Access should be denied
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("null or empty")

func test_rejects_malformed_path() -> void:
	# GIVEN: Path without protocol prefix
	var path = "data/config.txt"

	# WHEN: Validating access
	var result = _adapter.ValidateAndAudit(path, READ_MODE, "test_rejects_malformed_path")

	# THEN: Access should be denied (no res:// or user:// prefix)
	assert_bool(result.IsAllowed).is_false()
	assert_str(result.RejectionReason).contains("Invalid protocol")

# ============================================================================
# AUDIT Scenarios (3 tests - exceeds minimum requirement of 2)
# ============================================================================

func test_audit_log_written_on_rejection() -> void:
	# GIVEN: Invalid path that will be rejected
	var path = "C:/Windows/System32/evil.exe"

	# WHEN: Validating access (should write to audit log)
	var _result = _adapter.ValidateAndAudit(path, READ_MODE, "test_audit_log_written_on_rejection")

	# THEN: Audit log file should exist
	await get_tree().create_timer(0.1).timeout  # Small delay for file write
	assert_bool(FileAccess.file_exists(_test_audit_log)).is_true()

func test_audit_log_contains_required_fields() -> void:
	# GIVEN: Invalid path that will be rejected
	var path = "/etc/passwd"
	var caller = "test_audit_log_contains_required_fields"

	# WHEN: Validating access
	var _result = _adapter.ValidateAndAudit(path, READ_MODE, caller)

	# THEN: Audit log should contain JSONL with required fields
	await get_tree().create_timer(0.1).timeout  # Small delay for file write

	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	assert_object(file).is_not_null()

	var log_line = file.get_line()
	file.close()

	# Parse JSONL
	var json = JSON.new()
	var parse_result = json.parse(log_line)
	assert_int(parse_result).is_equal(OK)

	var log_entry = json.get_data()
	assert_object(log_entry).is_not_null()

	# Verify required fields (ADR-0004 event format)
	assert_that(log_entry.has("ts")).is_true()  # Timestamp
	assert_that(log_entry.has("action")).is_true()  # Event type
	assert_that(log_entry.has("reason")).is_true()  # Rejection reason
	assert_that(log_entry.has("target")).is_true()  # Path
	assert_that(log_entry.has("caller")).is_true()  # Caller context

	# Verify event naming convention (domain.entity.verb)
	assert_str(log_entry["action"]).is_equal("security.file.rejected")

	# Verify caller tracking
	assert_str(log_entry["caller"]).is_equal(caller)

func test_audit_log_multiple_rejections() -> void:
	# GIVEN: Multiple invalid paths
	var paths = [
		"C:/Windows/System32/evil1.exe",
		"/etc/passwd",
		"user://../../../etc/shadow"
	]

	# WHEN: Validating all paths (all should be rejected and logged)
	for path in paths:
		var _result = _adapter.ValidateAndAudit(path, READ_MODE, "test_audit_log_multiple_rejections")

	# THEN: Audit log should contain multiple JSONL entries
	await get_tree().create_timer(0.1).timeout  # Small delay for file writes

	var file = FileAccess.open(_test_audit_log, FileAccess.READ)
	assert_object(file).is_not_null()

	var line_count = 0
	while not file.eof_reached():
		var line = file.get_line()
		if line.strip_edges() != "":
			line_count += 1

	file.close()

	# Should have 3 JSONL entries (one per rejected path)
	assert_int(line_count).is_equal(3)

# ============================================================================
# Additional Edge Case Tests (bonus coverage)
# ============================================================================

func test_normalize_path_handles_mixed_separators() -> void:
	# GIVEN: Path with mixed forward/backward slashes
	var path = "user://data\\saves/player1\\config.txt"

	# WHEN: Normalizing path
	var normalized = _adapter.NormalizePath(path)

	# THEN: All separators should be unified to forward slash
	assert_str(normalized).not_contains("\\")
	assert_str(normalized).contains("user://data/saves/player1/config.txt")

func test_normalize_path_handles_url_encoding() -> void:
	# GIVEN: URL-encoded path
	var path = "user://%2e%2e/data.txt"

	# WHEN: Normalizing path
	var normalized = _adapter.NormalizePath(path)

	# THEN: URL encoding should be decoded
	assert_str(normalized).contains("..")

func test_allows_all_default_extensions() -> void:
	# GIVEN: Files with all default allowed extensions
	var extensions = [".txt", ".json", ".cfg", ".dat", ".sav"]

	# WHEN: Validating each extension
	for ext in extensions:
		var path = "user://test" + ext
		var result = _adapter.IsPathAllowed(path, READ_MODE)

		# THEN: All default extensions should be allowed
		assert_bool(result).is_true()
