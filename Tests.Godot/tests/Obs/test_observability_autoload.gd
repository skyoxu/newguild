extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _find_autoload_entries() -> Dictionary:
	var entries: Dictionary = {}
	for p in ProjectSettings.get_property_list():
		var n = p.get("name", "")
		if typeof(n) == TYPE_STRING and String(n).begins_with("autoload/"):
			entries[String(n)] = ProjectSettings.get_setting(String(n))
	return entries

func _normalize_autoload_path(value) -> String:
	if typeof(value) != TYPE_STRING:
		return ""
	var s := String(value)
	if s.begins_with("*"):
		s = s.substr(1)
	return s

func _is_script_path(path: String) -> bool:
	var p := path.to_lower()
	return p.begins_with("res://") and (p.ends_with(".cs") or p.ends_with(".gd"))

func _guess_observability_autoload_names(entries: Dictionary) -> Array[String]:
	var names: Array[String] = []
	for key in entries.keys():
		var full_key := String(key)
		var name := full_key.substr("autoload/".length())
		var path := _normalize_autoload_path(entries[key])
		var name_l := name.to_lower()
		var path_l := path.to_lower()
		if name_l.find("observability") != -1 or name_l.find("sentry") != -1:
			names.append(name)
		elif path_l.find("observability") != -1 or path_l.find("sentry") != -1:
			names.append(name)
	return names

# ACC:T24.1
func test_observability_related_autoload_smoke() -> void:
	assert_bool(ProjectSettings.has_setting("application/config/name")).is_true()

	var entries := _find_autoload_entries()
	assert_bool(entries.has("autoload/Observability")).is_true()
	assert_bool(entries.has("autoload/SentryClient")).is_true()

	var candidates := ["Observability", "SentryClient"]

	var root := get_tree().root
	for name in candidates:
		var setting_key := "autoload/" + name
		assert_bool(ProjectSettings.has_setting(setting_key)).is_true()
		var configured := _normalize_autoload_path(ProjectSettings.get_setting(setting_key))
		assert_bool(_is_script_path(configured)).is_true()

		assert_bool(root.has_node(name)).is_true()
		var node := root.get_node(name)
		assert_bool(node is Node).is_true()
