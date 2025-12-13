extends RefCounted
class_name SecurityProcessAdapterFactory
## GDScript wrapper for SecurityProcessAdapterFactory C# class.
## Provides convenient factory methods for creating SecurityProcessAdapter instances.
## Handles OS.execute security validation with command whitelist enforcement.
##
## @tutorial: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
## @experimental

## Reference to the C# SecurityProcessAdapterFactory class
const CSharpFactory = preload("res://Game.Godot/Adapters/Security/SecurityProcessAdapterFactory.cs")

## Creates SecurityProcessAdapter with default configuration.
## - Default allowed commands: git, dotnet, py, python, python3
## - No audit logging
##
## @param bus: InMemoryEventBus instance (required)
## @param audit_log_path: Optional path to JSONL audit log file (use "" for no logging)
## @return: SecurityProcessAdapter instance
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var adapter = SecurityProcessAdapterFactoryWrapper.create_with_defaults(bus)
static func create_with_defaults(bus: RefCounted, audit_log_path = "") -> RefCounted:
	if bus == null:
		push_error("SecurityProcessAdapterFactory: Event bus cannot be null")
		return null

	# Handle optional audit_log_path (empty string or null -> null for C#)
	var cs_audit_path = null
	if audit_log_path != null and not audit_log_path.is_empty():
		cs_audit_path = audit_log_path

	var factory = CSharpFactory.new()
	return factory.CreateWithDefaultsInstance(bus, cs_audit_path)

## Creates SecurityProcessAdapter with custom command whitelist.
##
## @param bus: InMemoryEventBus instance (required)
## @param allowed_commands: Array of allowed command names (e.g., ["git", "dotnet"])
## @param audit_log_path: Optional path to JSONL audit log file (use "" for no logging)
## @return: SecurityProcessAdapter instance
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var commands = ["git", "dotnet"]
##   var adapter = SecurityProcessAdapterFactoryWrapper.create_with_whitelist(bus, commands)
static func create_with_whitelist(bus: RefCounted, allowed_commands: Array, audit_log_path = "") -> RefCounted:
	if bus == null:
		push_error("SecurityProcessAdapterFactory: Event bus cannot be null")
		return null

	if allowed_commands.is_empty():
		push_error("SecurityProcessAdapterFactory: Command whitelist cannot be empty")
		return null

	# Handle optional audit_log_path (empty string or null -> null for C#)
	var cs_audit_path = null
	if audit_log_path != null and not audit_log_path.is_empty():
		cs_audit_path = audit_log_path

	# Use CreateFromGodotArrayInstance which handles the array conversion internally
	var factory = CSharpFactory.new()
	return factory.CreateFromGodotArrayInstance(bus, allowed_commands, cs_audit_path)

## Creates SecurityProcessAdapter for testing scenarios.
## - Uses default command whitelist
## - Assumes GD_SECURE_MODE=0 and SECURITY_TEST_MODE=1
## - Audit logging enabled to specified path
##
## @param bus: InMemoryEventBus instance (required)
## @param audit_log_path: Path to test audit log file (required for testing)
## @return: SecurityProcessAdapter instance configured for testing
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var adapter = SecurityProcessAdapterFactory.create_for_testing(bus, "user://test-audit.jsonl")
static func create_for_testing(bus: RefCounted, audit_log_path: String) -> RefCounted:
	if bus == null:
		push_error("SecurityProcessAdapterFactory: Event bus cannot be null")
		return null

	if audit_log_path.is_empty():
		push_error("SecurityProcessAdapterFactory: Test audit log path required")
		return null

	var factory = CSharpFactory.new()
	return factory.CreateForTestingInstance(bus, audit_log_path)
