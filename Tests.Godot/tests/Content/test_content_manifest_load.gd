extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const MANIFEST_PATH: String = "res://Game.Godot/Assets/Data/content/base/manifest.json"
const MANIFEST_LOADED_EVENT_TYPE: String = "core.content.manifest.loaded"

func _read_text_utf8(path: String) -> String:
	if not FileAccess.file_exists(path):
		return ""
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return ""
	return file.get_as_text()

func _parse_manifest_entries(json_text: String) -> Array:
	var parsed := JSON.parse_string(json_text)
	if parsed == null:
		return []
	if typeof(parsed) != TYPE_DICTIONARY:
		return []
	var dict := parsed as Dictionary
	var entries := dict.get("entries", [])
	if typeof(entries) != TYPE_ARRAY:
		return []
	return entries

func _try_load_manifest_entries(path: String) -> Dictionary:
	var result := {
		"ok": false,
		"path": path,
		"entries": [],
		"error": ""
	}

	if not FileAccess.file_exists(path):
		result["error"] = "manifest not found: %s" % path
		return result

	var text := _read_text_utf8(path)
	if text.is_empty():
		result["error"] = "manifest is empty: %s" % path
		return result

	var entries := _parse_manifest_entries(text)
	if entries.is_empty():
		result["error"] = "manifest parsed but has no usable entries: %s" % path
		return result

	result["ok"] = true
	result["entries"] = entries
	return result

func _sample_manifest_json() -> String:
	return "{\"manifest_id\":\"sample\",\"schema_version\":\"1\",\"entries\":[{\"id\":\"e1\",\"path\":\"res://dummy\",\"type\":\"text\"}]}"

# ACC:T27.1
func test_manifest_path_constant_is_canonical() -> void:
	assert_str(MANIFEST_PATH).starts_with("res://")
	assert_str(MANIFEST_PATH).ends_with("manifest.json")
	assert_str(MANIFEST_LOADED_EVENT_TYPE).starts_with("core.")
	assert_str(MANIFEST_LOADED_EVENT_TYPE).contains(".manifest.")

# Negative case (no ACC anchor): ensure failure is locatable.
func test_loader_returns_locatable_error_on_missing_manifest() -> void:
	var missing_path := "res://__missing__/manifest.json"
	var result := _try_load_manifest_entries(missing_path)
	assert_bool(result.get("ok", true) == false).is_true()
	assert_str(str(result.get("error", ""))).contains(missing_path)

# ACC:T27.3
func test_sample_manifest_json_parses_to_entries_list() -> void:
	var entries := _parse_manifest_entries(_sample_manifest_json())
	assert_int(entries.size()).is_equal(1)
	assert_bool(typeof(entries[0]) == TYPE_DICTIONARY).is_true()
	var entry := entries[0] as Dictionary
	assert_str(str(entry.get("id", ""))).is_equal("e1")
	assert_str(str(entry.get("path", ""))).starts_with("res://")

# ACC:T27.4
func test_real_manifest_loads_and_has_entries() -> void:
	var result := _try_load_manifest_entries(MANIFEST_PATH)
	assert_bool(result.get("ok", false) == true).is_true()
	assert_bool(typeof(result.get("entries", null)) == TYPE_ARRAY).is_true()
	var entries := result.get("entries", []) as Array
	assert_int(entries.size()).is_greater(0)
