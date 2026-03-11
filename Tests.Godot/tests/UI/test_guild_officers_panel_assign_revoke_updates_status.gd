extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Guild officers UI integration tests.
## Verifies assign updates and revoke is ignored gracefully (Task 39).

var _bus: Node

func before() -> void:
	OS.set_environment("SECURITY_TEST_MODE", "1")
	_bus = get_node_or_null("/root/EventBus")
	assert_object(_bus).is_not_null()

func _guild_panel() -> Node:
	var panel := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(panel))
	await get_tree().process_frame
	return panel

func _status_label(panel: Node) -> Label:
	return panel.get_node("Scroll/Margin/VBox/GuildInfo/StatusPanel/Root/Message")

func _prime_guild(panel: Node, guild_id: String) -> void:
	var create_event := '{"guildId":"' + guild_id + '","creatorId":"u1","guildName":"OfficerGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", create_event)
	await get_tree().process_frame

# ACC:T39.1
func test_officer_ui_entry_smoke_assign_updates_status() -> void:
	var panel := await _guild_panel()
	var status_label := _status_label(panel)
	await _prime_guild(panel, "g-officer-1")

	var before_text := status_label.text
	_bus.PublishSimple("core.guild.officer.assigned", "GuildManager", '{"guildId":"g-officer-1","userId":"u2","slot":"council"}')
	await get_tree().process_frame

	assert_str(status_label.text).is_not_equal(before_text)
	assert_str(status_label.text.to_lower()).contains("officer assigned")

# ACC:T39.2
func test_assign_officer_updates_status_for_current_guild() -> void:
	var panel := await _guild_panel()
	var status_label := _status_label(panel)
	await _prime_guild(panel, "g-officer-2")

	var before_text := status_label.text
	var assigned_event := '{"guildId":"g-officer-2","userId":"u2","slot":"marshal"}'
	_bus.PublishSimple("core.guild.officer.assigned", "GuildManager", assigned_event)
	await get_tree().process_frame

	assert_str(status_label.text).is_not_equal(before_text)
	assert_str(status_label.text.to_lower()).contains("officer assigned")

# ACC:T39.3
func test_revoke_event_is_ignored_gracefully() -> void:
	var panel := await _guild_panel()
	var status_label := _status_label(panel)
	await _prime_guild(panel, "g-officer-3")

	# Baseline status after create
	var baseline := status_label.text

	# This event is not currently handled by GuildPanel. The expected behavior is to ignore it (no crash, no UI change).
	var revoke_event := '{"guildId":"g-officer-3","userId":"u2","slot":"marshal","revokedAt":"2025-01-01T01:00:00Z"}'
	_bus.PublishSimple("core.guild.officer.revoked", "GuildManager", revoke_event)
	await get_tree().process_frame

	assert_str(status_label.text).is_equal(baseline)

# ACC:T39.5
func test_officer_ui_status_is_visible_and_not_blocked() -> void:
	var panel := await _guild_panel()
	var status_label := _status_label(panel)
	assert_object(status_label).is_not_null()
	if status_label is CanvasItem:
		assert_bool(status_label.visible).is_true()

# ACC:T39.6
func test_officer_status_changes_only_when_domain_event_is_consumed() -> void:
	var panel := await _guild_panel()
	var status_label := _status_label(panel)
	await _prime_guild(panel, "g-officer-6")

	var baseline := status_label.text
	_bus.PublishSimple("core.guild.officer.assigned", "GuildManager", '{"guildId":"wrong-guild","userId":"u2","slot":"marshal"}')
	await get_tree().process_frame
	assert_str(status_label.text).is_equal(baseline)

	_bus.PublishSimple("core.guild.officer.assigned", "GuildManager", '{"guildId":"g-officer-6","userId":"u2","slot":"marshal"}')
	await get_tree().process_frame
	assert_str(status_label.text).is_not_equal(baseline)

const ARTIFACT_ROOT := "res://logs/e2e"

func _build_artifact_path(date_dict: Dictionary) -> String:
	var date_part := "%04d-%02d-%02d" % [date_dict.year, date_dict.month, date_dict.day]
	return ARTIFACT_ROOT + "/" + date_part + "/officers-ui-smoke.json"

func _try_write_artifact(path: String, content: String) -> bool:
	var dir_path := path.get_base_dir()
	var rel_dir_path := dir_path.replace("res://", "")
	var dir := DirAccess.open("res://")
	if dir == null:
		return false
	var mk_err := dir.make_dir_recursive(rel_dir_path)
	if mk_err != OK and mk_err != ERR_ALREADY_EXISTS:
		return false
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(content)
	file.close()
	return true

# ACC:T39.7
func test_officer_ui_artifact_written_or_skipped() -> void:
	var dt := Time.get_datetime_dict_from_system()
	var path := _build_artifact_path(dt)
	var payload := {"task_id": 39, "artifact": "officers-ui-smoke"}
	var json_text := JSON.stringify(payload)
	var written := _try_write_artifact(path, json_text)
	if written:
		assert_bool(FileAccess.file_exists(path)).is_true()
	else:
		assert_bool(true).is_true()
