extends GdUnitTestSuite

## GdUnit4 security test suite for SecurityUrlAdapter (C# adapter)
## Tests URL validation with whitelist enforcement per ADR-0019
##
## Test Coverage:
## - Allow: Whitelisted HTTPS URLs pass validation
## - Deny: Non-HTTPS, dangerous schemes, non-whitelisted domains rejected
## - Invalid: Malformed URLs handled gracefully
## - SSRF: Null/empty whitelist rejects ALL URLs (CWE-918 prevention)
## - Audit: Rejection events logged to security-audit.jsonl

# Reference to GDScript wrapper for SecurityUrlAdapterFactory
const SecurityUrlAdapterFactory = preload("res://Game.Godot/Adapters/Security/security_url_adapter_factory.gd")

func before_test() -> void:
	# Ensure clean state before each test
	# Note: audit log cleanup handled by adapter's date-based path
	pass

## Test ALLOW scenario: Whitelisted HTTPS URL should pass
func test_allows_whitelisted_https_url() -> void:
	var allowed_hosts := ["example.com", "api.example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# Valid HTTPS URL in whitelist should be allowed
	var result: bool = adapter.IsUrlAllowed("https://example.com/api/v1/data")
	assert_bool(result).is_true()

	# Subdomain in whitelist should also work
	result = adapter.IsUrlAllowed("https://api.example.com/endpoint")
	assert_bool(result).is_true()

## Test DENY scenario: Non-HTTPS scheme should be rejected
func test_rejects_http_scheme() -> void:
	var allowed_hosts := ["example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# HTTP should be rejected (only HTTPS allowed per ADR-0019)
	var result: bool = adapter.IsUrlAllowed("http://example.com/api")
	assert_bool(result).is_false()

	# Verify rejection reason via ValidateAndAudit
	var audit_result = adapter.ValidateAndAudit("http://example.com/api", "test_rejects_http_scheme")
	assert_bool(audit_result.Get(0)).is_false()  # IsAllowed = false
	assert_str(audit_result.Get(1)).contains("Non-HTTPS")

## Test DENY scenario: Dangerous schemes should be blocked
func test_rejects_dangerous_schemes() -> void:
	var allowed_hosts := ["example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# javascript: scheme (XSS vector)
	var result: bool = adapter.IsUrlAllowed("javascript:alert(1)")
	assert_bool(result).is_false()

	# file:// scheme (file system access)
	result = adapter.IsUrlAllowed("file:///etc/passwd")
	assert_bool(result).is_false()

	# data: scheme (data URI injection)
	result = adapter.IsUrlAllowed("data:text/html,<script>alert(1)</script>")
	assert_bool(result).is_false()

	# blob: scheme (blob URL manipulation)
	result = adapter.IsUrlAllowed("blob:https://example.com/uuid")
	assert_bool(result).is_false()

## Test DENY scenario: Domain not in whitelist should be rejected
func test_rejects_non_whitelisted_domain() -> void:
	var allowed_hosts := ["example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# Different domain not in whitelist
	var result: bool = adapter.IsUrlAllowed("https://evil.com/api")
	assert_bool(result).is_false()

	# Similar but different domain
	result = adapter.IsUrlAllowed("https://example.org")
	assert_bool(result).is_false()

	# Verify rejection reason
	var audit_result = adapter.ValidateAndAudit("https://evil.com/api", "test_rejects_non_whitelisted_domain")
	assert_bool(audit_result.Get(0)).is_false()
	assert_str(audit_result.Get(1)).contains("not in whitelist")

## Test SSRF prevention: Null whitelist should reject ALL URLs (CWE-918)
func test_rejects_all_urls_when_whitelist_null() -> void:
	# CRITICAL SECURITY TEST: Null whitelist must reject everything
	var adapter = SecurityUrlAdapterFactory.create_with_ssrf_protection(null)

	# Even valid HTTPS URL should be rejected when whitelist not configured
	var result: bool = adapter.IsUrlAllowed("https://example.com/api")
	assert_bool(result).is_false()

	# Verify rejection reason mentions SSRF prevention
	var audit_result = adapter.ValidateAndAudit("https://example.com/api", "test_rejects_all_urls_when_whitelist_null")
	assert_bool(audit_result.Get(0)).is_false()
	assert_str(audit_result.Get(1)).contains("SSRF prevention")

## Test SSRF prevention: Empty whitelist should reject ALL URLs
func test_rejects_all_urls_when_whitelist_empty() -> void:
	var adapter = SecurityUrlAdapterFactory.create_from_godot_array([], null)

	# Empty whitelist should have same behavior as null
	var result: bool = adapter.IsUrlAllowed("https://example.com/api")
	assert_bool(result).is_false()

	var audit_result = adapter.ValidateAndAudit("https://example.com/api", "test_rejects_all_urls_when_whitelist_empty")
	assert_bool(audit_result.Get(0)).is_false()
	assert_str(audit_result.Get(1)).contains("SSRF prevention")

## Test INVALID scenario: Malformed URL should be handled gracefully
func test_rejects_invalid_uri_format() -> void:
	var allowed_hosts := ["example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# Not a valid URL
	var result: bool = adapter.IsUrlAllowed("not-a-url")
	assert_bool(result).is_false()

	# Missing scheme
	result = adapter.IsUrlAllowed("example.com/api")
	assert_bool(result).is_false()

	# Invalid characters
	result = adapter.IsUrlAllowed("https://exam ple.com/api")
	assert_bool(result).is_false()

## Test INVALID scenario: Null or empty URL should be rejected
func test_rejects_null_or_empty_url() -> void:
	var allowed_hosts := ["example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# Empty string
	var result: bool = adapter.IsUrlAllowed("")
	assert_bool(result).is_false()

	# Whitespace only
	result = adapter.IsUrlAllowed("   ")
	assert_bool(result).is_false()

	# Verify rejection reason
	var audit_result = adapter.ValidateAndAudit("", "test_rejects_null_or_empty_url")
	assert_bool(audit_result.Get(0)).is_false()
	assert_str(audit_result.Get(1)).contains("null or empty")

## Test AUDIT: Verify AllowedHosts property exposes whitelist
func test_exposes_allowed_hosts_property() -> void:
	var allowed_hosts := ["example.com", "api.example.com"]
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, null)

	# AllowedHosts property should return the configured whitelist
	var exposed_hosts = adapter.AllowedHosts
	assert_object(exposed_hosts).is_not_null()
	assert_int(exposed_hosts.size()).is_equal(2)

## Test AUDIT: Null whitelist should expose null AllowedHosts
func test_null_whitelist_exposes_null_property() -> void:
	var adapter = SecurityUrlAdapterFactory.create_with_ssrf_protection(null)

	# AllowedHosts should be null when whitelist not configured
	var exposed_hosts = adapter.AllowedHosts
	assert_object(exposed_hosts).is_null()

## Test AUDIT: Rejection should write to audit log (integration test)
## Note: This test verifies audit file creation but doesn't parse JSONL
## Manual verification: Check logs/ci/<date>/security-audit.jsonl after test run
func test_audit_log_written_on_rejection() -> void:
	var allowed_hosts := ["example.com"]
	# Use custom audit path for testing
	var test_audit_path := "logs/ci/test/security-audit-test.jsonl"
	var adapter = SecurityUrlAdapterFactory.create_with_whitelist(allowed_hosts, test_audit_path)

	# Trigger rejection with audit
	var result = adapter.ValidateAndAudit("https://evil.com/api", "test_audit_log_written_on_rejection")
	assert_bool(result.Get(0)).is_false()

	# Verify audit file exists
	# Note: File content validation (JSONL format, 5 fields) done by separate Python script
	var file := FileAccess.open(test_audit_path, FileAccess.READ)
	assert_object(file).is_not_null()
	if file:
		file.close()
