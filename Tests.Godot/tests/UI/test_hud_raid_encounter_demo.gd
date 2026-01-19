extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE_PATH: String = "res://Game.Godot/Scenes/UI/HUD.tscn"

var _prev_security_test_mode: String = ""
var _prev_enable_playable: String = ""
var _prev_secure_mode: String = ""
var _bus: Node = null
var _bus_cb: Callable = Callable()
var _types: Array[String] = []
var _security_ids: Array[String] = []
var _raid_resolved_reward_points: Array[int] = []
var _score_added: Array[int] = []
var _score_values: Array[int] = []

func _on_evt(type, _source, _data_json, id, _specVersion, _dataContentType, _timestampIso) -> void:
	var t := str(type)
	_types.append(t)
	if t == "security.raid_encounter_demo.decision":
		_security_ids.append(str(id))
	if t == "core.raid.resolved":
		var json := JSON.new()
		var rc := json.parse(str(_data_json))
		if rc == OK:
			var entry = json.get_data()
			if entry is Dictionary and entry.has("rewardPoints"):
				_raid_resolved_reward_points.append(int(entry["rewardPoints"]))
	if t == "core.score.changed":
		var json2 := JSON.new()
		var rc2 := json2.parse(str(_data_json))
		if rc2 == OK:
			var entry2 = json2.get_data()
			if entry2 is Dictionary:
				if entry2.has("added"):
					_score_added.append(int(entry2["added"]))
				if entry2.has("score"):
					_score_values.append(int(entry2["score"]))

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
	assert_bool(OS.get_environment("SECURITY_TEST_MODE") == "1").is_true()
	assert_bool(OS.get_environment("GD_ENABLE_PLAYABLE") == "1").is_true()

	# Best-effort cleanup: avoid cross-run coupling via persistent user:// logs.
	var date_utc := Time.get_date_string_from_system(true)
	var audit_dir := "user://logs/ci/%s" % date_utc
	if DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(audit_dir)):
		var d := DirAccess.open(audit_dir)
		if d != null:
			d.list_dir_begin()
			var entry_name := d.get_next()
			while entry_name != "":
				if entry_name.begins_with("security-audit") and entry_name.ends_with(".jsonl"):
					d.remove(entry_name)
				entry_name = d.get_next()
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
	if bus == null:
		return

	_types.clear()
	_security_ids.clear()
	_raid_resolved_reward_points.clear()
	_score_added.clear()
	_score_values.clear()
	var types := _types
	var security_ids := _security_ids
	var reward_points := _raid_resolved_reward_points

	_bus = bus
	_bus_cb = Callable(self, "_on_evt")

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
	var ok_phase1 := await _wait_for_type_count(types, "core.game_turn.phase_changed", phase_changed_before + 1)
	assert_bool(ok_phase1).is_true()
	if not ok_phase1:
		return

	phase_changed_before = _count_type(types, "core.game_turn.phase_changed")
	hud_scene.AdvanceTurnFromGd()
	var ok_phase2 := await _wait_for_type_count(types, "core.game_turn.phase_changed", phase_changed_before + 1)
	assert_bool(ok_phase2).is_true()
	if not ok_phase2:
		return

	var week_advanced_before := _count_type(types, "core.game_turn.week_advanced")
	hud_scene.AdvanceTurnFromGd()
	var ok_week := await _wait_for_type_count(types, "core.game_turn.week_advanced", week_advanced_before + 1)
	assert_bool(ok_week).is_true()
	if not ok_week:
		return

	assert_bool(types.has("core.game_turn.week_advanced")).is_true()

	hud_scene.TriggerRaidEncounterDemo()
	assert_bool(hud_scene.has_signal("RaidEncounterDemoCompleted")).is_true()
	var ok_security := await _wait_for_type_count(types, "security.raid_encounter_demo.decision", 1)
	assert_bool(ok_security).is_true()
	if not ok_security:
		return

	var result := str(hud_scene.RaidEncounterDemoLastResult)
	# The demo runner is deterministic and expected to succeed.
	assert_bool(result == "success").is_true()

	assert_bool(types.has("core.raid.scheduled")).is_true()
	assert_bool(types.has("core.raid.resolved")).is_true()
	assert_bool(types.has("security.raid_encounter_demo.decision")).is_true()
	assert_bool(reward_points.size() > 0).is_true()
	if reward_points.size() > 0:
		assert_bool(reward_points[reward_points.size() - 1] > 0).is_true()
	assert_bool(types.has("core.score.changed")).is_true()
	assert_bool(_score_added.size() > 0).is_true()
	assert_bool(_score_values.size() > 0).is_true()
	if _score_added.size() > 0 and reward_points.size() > 0:
		assert_int(_score_added[_score_added.size() - 1]).is_equal(reward_points[reward_points.size() - 1])
	if _score_values.size() > 0 and _score_added.size() > 0:
		assert_bool(_score_values[_score_values.size() - 1] >= _score_added[_score_added.size() - 1]).is_true()
	assert_bool(security_ids.size() > 0).is_true()
	if security_ids.size() == 0:
		return

	var date_utc := Time.get_date_string_from_system(true)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	assert_bool(await _wait_for_audit_contains(audit_path, "security.raid_encounter_demo.decision")).is_true()
	assert_bool(await _wait_for_audit_contains(audit_path, security_ids[security_ids.size() - 1])).is_true()

# ACC:T17.3
# UI smoke: demo gate deny path is observable and auditable.
func test_hud_demo_gate_denies_when_disabled() -> void:
	OS.set_environment("GD_ENABLE_PLAYABLE", "0")
	assert_bool(OS.get_environment("GD_ENABLE_PLAYABLE") == "0").is_true()

	var bus = get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()
	if bus == null:
		return

	_types.clear()
	_security_ids.clear()
	_raid_resolved_reward_points.clear()
	_score_added.clear()
	_score_values.clear()
	var types := _types
	var security_ids := _security_ids

	_bus = bus
	_bus_cb = Callable(self, "_on_evt")

	if not bus.is_connected("DomainEventEmitted", _bus_cb):
		bus.connect("DomainEventEmitted", _bus_cb)

	var hud_scene := preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(auto_free(hud_scene))
	await get_tree().process_frame

	hud_scene.TriggerRaidEncounterDemo()
	var ok_security := await _wait_for_type_count(types, "security.raid_encounter_demo.decision", 1)
	assert_bool(ok_security).is_true()
	if not ok_security:
		return

	var result := str(hud_scene.RaidEncounterDemoLastResult)
	assert_bool(result == "denied").is_true()
	assert_bool(types.has("security.raid_encounter_demo.decision")).is_true()
	assert_bool(security_ids.size() > 0).is_true()
	if security_ids.size() == 0:
		return

	var date_utc := Time.get_date_string_from_system(true)
	var audit_path := "user://logs/ci/%s/security-audit.jsonl" % date_utc
	assert_bool(await _wait_for_audit_contains(audit_path, security_ids[security_ids.size() - 1])).is_true()
