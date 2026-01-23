extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const CONTRACTS_DIR := "res://Game.Core/Contracts"
const MAIN_SCENE_SETTING_KEY := "application/run/main_scene"

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
	assert_bool(main_scene_path != "").is_true()
	assert_bool(ResourceLoader.exists(main_scene_path)).is_true()

	var packed := load(main_scene_path)
	assert_bool(packed != null).is_true()
	assert_bool(packed is PackedScene).is_true()

	var instance := (packed as PackedScene).instantiate()
	assert_bool(instance != null).is_true()
	assert_bool(instance is Node).is_true()

	get_tree().root.add_child(instance)
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
		assert_int(legacy_hits.size()).is_equal(0).override_failure_message("Legacy event prefix candidates detected: %s" % str(legacy_hits))

	(instance as Node).queue_free()
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
	for t in event_types:
		assert_bool(t.begins_with("core.")).is_true()
		assert_bool(t.begins_with("game.")).is_false()
