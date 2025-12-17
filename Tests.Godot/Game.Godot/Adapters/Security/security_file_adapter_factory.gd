extends RefCounted
class_name SecurityFileAdapterFactory
## GDScript wrapper for SecurityFileAdapterFactory C# class.
## Provides convenient factory methods for creating SecurityFileAdapter instances.
## Handles type conversions between GDScript and C# (arrays, enums, etc.).
##
## @tutorial: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
## @experimental

## Reference to the C# SecurityFileAdapterFactory class
const CSharpFactory = preload("res://Game.Godot/Adapters/Security/SecurityFileAdapterFactory.cs")

## File access modes (matches ISecurityFileValidator.FileAccessMode enum)
enum FileAccessMode {
	READ = 0,   ## Read-only access (allowed for res:// and user://)
	WRITE = 1   ## Write access (only allowed for user://)
}

## Creates SecurityFileAdapter with default configuration.
## - Default extensions: .txt, .json, .cfg, .dat, .sav
## - Default max file size: 10MB
## - No audit logging
##
## @param bus: InMemoryEventBus instance (required)
## @return: SecurityFileAdapter instance
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var adapter = SecurityFileAdapterFactoryWrapper.create_default(bus)
static func create_default(bus: RefCounted) -> RefCounted:
	if bus == null:
		push_error("SecurityFileAdapterFactoryWrapper: Event bus cannot be null")
		return null

	var factory = CSharpFactory.new()
	return factory.CreateDefaultInstance(bus)

## Creates SecurityFileAdapter with custom extension whitelist.
## Uses default max file size (10MB) and no audit logging.
##
## @param bus: InMemoryEventBus instance (required)
## @param allowed_extensions: Array of allowed file extensions (e.g., [".txt", ".json"])
## @return: SecurityFileAdapter instance
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var extensions = [".txt", ".json", ".cfg"]
##   var adapter = SecurityFileAdapterFactoryWrapper.create_with_extensions(bus, extensions)
static func create_with_extensions(bus: RefCounted, allowed_extensions: Array) -> RefCounted:
	if bus == null:
		push_error("SecurityFileAdapterFactoryWrapper: Event bus cannot be null")
		return null

	if allowed_extensions.is_empty():
		push_error("SecurityFileAdapterFactoryWrapper: Extension whitelist cannot be empty")
		return null

	# Convert GDScript Array to C# string[]
	var cs_extensions = _gdscript_array_to_csharp_string_array(allowed_extensions)

	var factory = CSharpFactory.new()
	return factory.CreateWithExtensionsInstance(bus, cs_extensions)

## Creates SecurityFileAdapter with audit logging enabled.
## Uses default extensions and max file size.
##
## @param bus: InMemoryEventBus instance (required)
## @param audit_log_path: Path to JSONL audit log file (e.g., "user://logs/security-audit.jsonl")
## @return: SecurityFileAdapter instance
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var adapter = SecurityFileAdapterFactoryWrapper.create_with_audit(bus, "user://logs/security-audit.jsonl")
static func create_with_audit(bus: RefCounted, audit_log_path) -> RefCounted:
	if bus == null:
		push_error("SecurityFileAdapterFactory: Event bus cannot be null")
		return null

	if audit_log_path == null or (audit_log_path is String and audit_log_path.is_empty()):
		push_error("SecurityFileAdapterFactory: Audit log path cannot be null or empty")
		return null

	var factory = CSharpFactory.new()
	return factory.CreateWithAuditInstance(bus, audit_log_path)

## Creates SecurityFileAdapter with full custom configuration.
##
## @param bus: InMemoryEventBus instance (required)
## @param allowed_extensions: Array of allowed file extensions
## @param max_file_size: Maximum file size in bytes (must be positive)
## @param audit_log_path: Path to JSONL audit log file (optional, can be null)
## @return: SecurityFileAdapter instance
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var extensions = [".txt", ".dat"]
##   var max_size = 5 * 1024 * 1024  # 5MB
##   var adapter = SecurityFileAdapterFactoryWrapper.create_custom(bus, extensions, max_size, "user://logs/audit.jsonl")
static func create_custom(bus: RefCounted, allowed_extensions: Array, max_file_size: int, audit_log_path: String = "") -> RefCounted:
	if bus == null:
		push_error("SecurityFileAdapterFactoryWrapper: Event bus cannot be null")
		return null

	if allowed_extensions.is_empty():
		push_error("SecurityFileAdapterFactoryWrapper: Extension whitelist cannot be empty")
		return null

	if max_file_size <= 0:
		push_error("SecurityFileAdapterFactoryWrapper: Max file size must be positive")
		return null

	# Convert GDScript Array to C# string[]
	var cs_extensions = _gdscript_array_to_csharp_string_array(allowed_extensions)

	# Handle optional audit_log_path (empty string -> null for C#)
	var cs_audit_path = null if audit_log_path.is_empty() else audit_log_path

	var factory = CSharpFactory.new()
	return factory.CreateCustomInstance(bus, cs_extensions, max_file_size, cs_audit_path)

## Creates SecurityFileAdapter for testing scenarios.
## - Restricted extensions: .txt only
## - Small file size limit: 1MB
## - Audit logging enabled to specified path
##
## @param bus: InMemoryEventBus instance (required)
## @param audit_log_path: Path to test audit log file (required for testing)
## @return: SecurityFileAdapter instance configured for testing
##
## Example:
##   var bus = InMemoryEventBus.new()
##   var adapter = SecurityFileAdapterFactoryWrapper.create_for_testing(bus, "user://test-audit.jsonl")
static func create_for_testing(bus: RefCounted, audit_log_path: String) -> RefCounted:
	if bus == null:
		push_error("SecurityFileAdapterFactoryWrapper: Event bus cannot be null")
		return null

	if audit_log_path.is_empty():
		push_error("SecurityFileAdapterFactoryWrapper: Test audit log path required")
		return null

	var factory = CSharpFactory.new()
	return factory.CreateForTestingInstance(bus, audit_log_path)

## Helper function: Converts GDScript Array to C# string[]
## GDScript arrays need explicit conversion to C# typed arrays.
##
## @param gd_array: GDScript Array of Strings
## @return: C# string[] array
static func _gdscript_array_to_csharp_string_array(gd_array: Array) -> Array:
	# Godot 4.x automatically handles Array -> C# string[] conversion
	# when calling C# methods, but we validate types here for safety
	var result = []
	for item in gd_array:
		if typeof(item) != TYPE_STRING:
			push_warning("SecurityFileAdapterFactoryWrapper: Non-string item in extension array, converting to string")
		result.append(str(item))
	return result
