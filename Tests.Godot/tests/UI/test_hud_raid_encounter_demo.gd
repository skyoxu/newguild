extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE_PATH: String = "res://Game.Godot/Scenes/UI/HUD.tscn"

var _prev_security_test_mode: String = ""
var _prev_enable_playable: String = ""
var _prev_secure_mode: String = ""
var _bus: Node = null
var _bus_cb: Callable = Callable()

func _count_type(types: Array[String], wanted: String) -> int:
	var c := 0
	for t in types:
		if t == wanted:
			c += 1
	return c

func _wait_for_type_count(types: Array[String], wanted: String, min_count: int, frames: int = 240) -> bool:
	for i in range(frames):
		if _count_type(types, wanted) >= min_count:
			return true
		await get_tree().process_frame
	return false

func _wait_for_audit_contains(path: String, token: String, frames: int = 240) -> bool:
	for i in range(frames):
		if FileAccess.file_exists(path):
			var txt := FileAccess.get_file_as_string(path)
			if txt.contains(token):
				return true
		await get_tree().process_frame
	return false

func before() -> void:
	_prev_security_test_mode = OS.get_environment("SECURITY_TEST_MODE")
	_prev_enable_playable = OS.get_environment("GD_ENABLE_PLAYABLE")
	_prev_secure_mode = OS.get_environment("GD_SECURE_MODE")
	OS.set_environment("SECURITY_TEST_MODE", "1")
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	OS.set_environment("GD_SECURE_MODE", "0")

	# Best-effort cleanup: avoid cross-run coupling via persistent user:// logs.
	var date_utc := Time.get_date_string_from_system(true)
	var audit_dir := "user://logs/ci/%s" % date_utc
	if DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(audit_dir)):
		var d := DirAccess.open(audit_dir)
		if d != null:
			d.list_dir_begin()
			var name := d.get_next()
			while name != "":
				if name.begins_with("security-audit") and name.ends_with(".jsonl"):
					d.remove(name)
				name = d.get_next()
			d.list_dir_end()

	var existing = get_node_or_null("/root/EventBus")
	if existing != null:
		assert_bool(existing.has_signal("DomainEventEmitted")).is_true()
		return

	var __bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	__bus.name = "EventBus"
	get_tree().get_root().add_child(auto_free(__bus))

func after() -> void:
	if _bus != null and _bus_cb.is_valid() and _bus.is_connected("DomainEventEmitted", _bus_cb):
		_bus.disconnect("DomainEventEmitted", _bus_cb)
	_bus = null
	_bus_cb = Callable()
	OS.set_environment("SECURITY_TEST_MODE", _prev_security_test_mode)
	OS.set_environment("GD_ENABLE_PLAYABLE", _prev_enable_playable)
	OS.set_environment("GD_SECURE_MODE", _prev_secure_mode)

# ACC:T17.3
# UI smoke: trigger one raid encounter demo and observe a concrete result.
func test_hud_can_trigger_raid_encounter_demo_and_expose_result() -> void:
	var bus = get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()

	var types: Array[String] = []
	var security_ids: Array[String] = []
	var security_timestamp_iso: String = ""
	var cb := func(type, _source, _data_json, id, _specVersion, _dataContentType, timestampIso) -> void:
		var t := str(type)
		types.append(t)
		if t == "security.raid_encounter_demo.decision":
			security_ids.append(str(id))
			security_timestamp_iso = str(timestampIso)

	_bus = bus
	_bus_cb = cb

	if not bus.is_connected("DomainEventEmitted", _bus_cb):
		bus.connect("DomainEventEmitted", _bus_cb)

	var hud_scene := preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(auto_free(hud_scene))
	await get_tree().process_frame

	assert_bool(hud_scene.has_method("TriggerRaidEncounterDemo")).is_true()
	assert_bool(hud_scene.has_method("AdvanceTurnFromGd")).is_true()

	# Drive the turn system enough to produce core.game_turn.week_advanced
	var phase_changed_before := _count_type(types, "core.game_turn.phase_changed")
	hud_scene.AdvanceTurnFromGd()
	assert_bool(await _wait_for_type_count(types, "core.game_turn.phase_changed", phase_changed_before + 1)).is_true()

	phase_changed_before = _count_type(types, "core.game_turn.phase_changed")
	hud_scene.AdvanceTurnFromGd()
	assert_bool(await _wait_for_type_count(types, "core.game_turn.phase_changed", phase_changed_before + 1)).is_true()

	var week_advanced_before := _count_type(types, "core.game_turn.week_advanced")
	hud_scene.AdvanceTurnFromGd()
	assert_bool(await _wait_for_type_count(types, "core.game_turn.week_advanced", week_advanced_before + 1)).is_true()

	assert_bool(types.has("core.game_turn.week_advanced")).is_true()

	hud_scene.TriggerRaidEncounterDemo()
	assert_bool(hud_scene.has_signal("RaidEncounterDemoCompleted")).is_true()
	await hud_scene.RaidEncounterDemoCompleted

	var result := str(hud_scene.RaidEncounterDemoLastResult)
	assert_bool(result == "success" or result == "failed").is_true()

	assert_bool(types.has("core.raid.scheduled")).is_true()
	assert_bool(types.has("core.raid.resolved")).is_true()
	assert_bool(types.has("security.raid_encounter_demo.decision")).is_true()
	assert_bool(security_ids.size() > 0).is_true()
	assert_bool(security_timestamp_iso.length() >= 10).is_true()

	var date_utc := security_timestamp_iso.substr(0, 10)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	assert_bool(await _wait_for_audit_contains(audit_path, "security.raid_encounter_demo.decision")).is_true()
	assert_bool(await _wait_for_audit_contains(audit_path, security_ids[security_ids.size() - 1])).is_true()

# ACC:T17.3
# UI smoke: demo gate deny path is observable and auditable.
func test_hud_demo_gate_denies_when_disabled() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "0")

	var bus = get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()

	var types: Array[String] = []
	var security_ids: Array[String] = []
	var security_timestamp_iso: String = ""
	var cb := func(type, _source, _data_json, id, _specVersion, _dataContentType, timestampIso) -> void:
		var t := str(type)
		types.append(t)
		if t == "security.raid_encounter_demo.decision":
			security_ids.append(str(id))
			security_timestamp_iso = str(timestampIso)

	_bus = bus
	_bus_cb = cb

	if not bus.is_connected("DomainEventEmitted", _bus_cb):
		bus.connect("DomainEventEmitted", _bus_cb)

	var hud_scene := preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(auto_free(hud_scene))
	await get_tree().process_frame

	hud_scene.TriggerRaidEncounterDemo()
	await hud_scene.RaidEncounterDemoCompleted

	var result := str(hud_scene.RaidEncounterDemoLastResult)
	assert_bool(result == "denied").is_true()
	assert_bool(types.has("security.raid_encounter_demo.decision")).is_true()
	assert_bool(security_ids.size() > 0).is_true()
	assert_bool(security_timestamp_iso.length() >= 10).is_true()

	var date_utc := security_timestamp_iso.substr(0, 10)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	assert_bool(await _wait_for_audit_contains(audit_path, security_ids[security_ids.size() - 1])).is_true()
