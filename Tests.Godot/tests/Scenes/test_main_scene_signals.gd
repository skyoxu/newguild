extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const MENU_TYPES := preload("res://Game.Godot/Scripts/UI/UiMenuEventTypes.gd")

const CONTRACTS_DIR := "res://Game.Core/Contracts"
const MAIN_SCENE_SETTING_KEY := "application/run/main_scene"

func after_test() -> void:
	# Allow queued frees from auto_free()/queue_free() to be processed before orphan detection.
	await get_tree().process_frame
	await get_tree().process_frame

func _read_text(path: String) -> String:
	if not FileAccess.file_exists(path):
		return ""
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return ""
	var text := f.get_as_text()
	f.close()
	return text

func _collect_cs_files(root: String) -> PackedStringArray:
	var out := PackedStringArray()
	var dir := DirAccess.open(root)
	if dir == null:
		return out
	dir.list_dir_begin()
	var name := dir.get_next()
	while name != "":
		if name == "." or name == "..":
			name = dir.get_next()
			continue
		var path := root.path_join(name)
		if dir.current_is_dir():
			out.append_array(_collect_cs_files(path))
		else:
			if name.to_lower().ends_with(".cs"):
				out.append(path)
		name = dir.get_next()
	dir.list_dir_end()
	return out

func _extract_event_types_from_cs(text: String) -> PackedStringArray:
	var out := PackedStringArray()
	var re := RegEx.new()
	var err := re.compile("EventType\\s*=\\s*\"([^\"]+)\"")
	if err != OK:
		return out
	for m in re.search_all(text):
		var value := m.get_string(1)
		if value != "":
			out.append(value)
	return out

func _scan_for_legacy_game_prefix(paths: PackedStringArray) -> Dictionary:
	var results := {}
	for p in paths:
		var text := _read_text(p)
		if text == "":
			continue
		var count := 0
		var idx := text.find("\"game.")
		while idx != -1:
			count += 1
			idx = text.find("\"game.", idx + 1)
		if count > 0:
			results[p] = count
	return results

# ACC:T42.10
# Smoke: main scene loads and runs at least one frame.
# This scaffold logs (but does not fail) on legacy "game." string occurrences in known sources.
func test_main_scene_smoke_loads_and_runs_one_frame() -> void:
	var main_scene_path := str(ProjectSettings.get_setting(MAIN_SCENE_SETTING_KEY, ""))
	# In the dedicated Tests.Godot project, `application/run/main_scene` can be unset or default to `res://`.
	# For smoke we prefer the actual game entry scene when present.
	if main_scene_path == "" or main_scene_path == "res://":
		main_scene_path = "res://Game.Godot/Scenes/Main.tscn"
	if not ResourceLoader.exists(main_scene_path):
		# Ensure the test fails without triggering a hard script error/debugger break.
		assert_bool(false).is_true()
		return

	var packed := load(main_scene_path)
	if packed == null or not (packed is PackedScene):
		assert_bool(false).is_true()
		return

	var instance := (packed as PackedScene).instantiate()
	if instance == null or not (instance is Node):
		assert_bool(false).is_true()
		return

	add_child(instance)
	auto_free(instance)
	await get_tree().process_frame
	assert_bool(is_instance_valid(instance)).is_true()

	var known_sources := PackedStringArray([
		"res://Game.Core/State/GameStateManager.cs",
		"res://Game.Core/Services/RaidEncounterStateMachine.cs",
		"res://Game.Godot/Scripts/UI/HUD.cs",
		"res://Game.Godot/Adapters/EventBusAdapter.cs",
	])
	var legacy_hits := _scan_for_legacy_game_prefix(known_sources)
	if legacy_hits.size() > 0:
		# Informational only: this scan is intentionally broad and may include non-event strings (e.g., CloudEvents `source`).
		push_warning("Legacy string candidates detected (scan=\\\"\\\"game.\\\"\\\"): %s" % str(legacy_hits))
	await get_tree().process_frame

func test_contract_eventtype_constants_use_core_prefix() -> void:
	var dir := DirAccess.open(CONTRACTS_DIR)
	assert_bool(dir != null).is_true()

	var cs_files := _collect_cs_files(CONTRACTS_DIR)
	assert_bool(cs_files.size() > 0).is_true()

	var event_types := PackedStringArray()
	for p in cs_files:
		event_types.append_array(_extract_event_types_from_cs(_read_text(p)))

	assert_bool(event_types.size() > 0).is_true()
	var allowed_prefixes := PackedStringArray(["core.", MENU_TYPES.PREFIX, "screen."])
	var invalid := PackedStringArray()
	for t in event_types:
		if t == "":
			invalid.append("<empty>")
			continue
		if t.begins_with("game."):
			invalid.append(t)
			continue
		var ok := false
		for prefix in allowed_prefixes:
			if t.begins_with(prefix):
				ok = true
				break
		if not ok:
			invalid.append(t)

	if invalid.size() > 0:
		push_error("Unexpected EventType values (allowed prefixes=%s): %s" % [str(allowed_prefixes), str(invalid)])
	assert_int(invalid.size()).is_equal(0)
