## GDScript wrapper for SecurityUrlAdapterFactory (C# implementation)
## Provides static snake_case methods for test integration per Godot conventions
##
## Design Rationale:
## - Test files expect static method calls: SecurityUrlAdapterFactory.create_with_whitelist()
## - C# factory has instance methods: factory.CreateWithWhitelist()
## - This wrapper bridges the gap by providing static GDScript methods
## - Each static method creates a C# factory instance and delegates the call
##
## Pattern: Thin wrapper for C# interop (ADR-0007 + Godot 4.5 C# conventions)

## Preload C# factory class (must inherit from RefCounted)
const CSharpFactory = preload("res://Game.Godot/Adapters/Security/SecurityUrlAdapterFactory.cs")

## Create SecurityUrlAdapter with whitelist configuration
## @param allowed_hosts: GDScript Array of allowed HTTPS domain names
## @param audit_log_path: Optional path to security audit JSONL file
## @return: ISecurityUrlValidator instance (C# object callable from GDScript)
static func create_with_whitelist(allowed_hosts: Array, audit_log_path) -> Object:
	var factory = CSharpFactory.new()
	# Godot auto-converts GDScript Array to C# string[] when calling C# methods
	return factory.CreateWithWhitelist(allowed_hosts, audit_log_path)

## Create SecurityUrlAdapter with SSRF protection (null whitelist)
## Rejects ALL external URLs as secure default per ADR-0019
## @param audit_log_path: Optional path to security audit JSONL file
## @return: ISecurityUrlValidator instance that rejects all URLs
static func create_with_ssrf_protection(audit_log_path) -> Object:
	var factory = CSharpFactory.new()
	return factory.CreateWithSsrfProtection(audit_log_path)

## Create SecurityUrlAdapter from Godot Array (handles empty/null arrays)
## @param godot_array: GDScript Array containing string hostnames
## @param audit_log_path: Optional audit log path
## @return: ISecurityUrlValidator instance
static func create_from_godot_array(godot_array: Array, audit_log_path) -> Object:
	var factory = CSharpFactory.new()
	# C# factory has special method for Godot.Collections.Array handling
	return factory.CreateFromGodotArray(godot_array, audit_log_path)

## Create SecurityUrlAdapter for testing with example.com whitelist
## WARNING: FOR TESTING ONLY - Do not use in production code
## @param audit_log_path: Optional audit log path for test verification
## @return: ISecurityUrlValidator configured with test whitelist
static func create_for_testing(audit_log_path) -> Object:
	var factory = CSharpFactory.new()
	return factory.CreateForTesting(audit_log_path)
