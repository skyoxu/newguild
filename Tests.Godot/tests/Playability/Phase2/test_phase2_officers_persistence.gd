extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: officer assignment + Save/Load + observable UI via Activity Feed.

const MAIN_SCENE := "res://Game.Godot/Scenes/Main.tscn"

func before() -> void:
	OS.set_environment("SECURITY_TEST_MODE", "1")

func _reset_root_node(node_name: String) -> void:
	var root := get_tree().get_root()
	var existing := root.get_node_or_null(node_name)
	if existing != null:
		existing.queue_free()
		await get_tree().process_frame

func _ensure_root_node(script_path: String, node_name: String) -> void:
	var root := get_tree().get_root()
	var existing := root.get_node_or_null(node_name)
	if existing != null:
		return
	var script = load(script_path)
	assert_that(script).is_not_null()
	var created = script.new()
	assert_that(created).is_not_null()
	created.name = node_name
	root.add_child(auto_free(created))
	await get_tree().process_frame

func _spawn_main_on_root() -> Node:
	var packed := load(MAIN_SCENE)
	assert_that(packed).is_not_null()
	var instance: Node = packed.instantiate()
	instance.name = "Main"

	var root := get_tree().root
	var existing := root.get_node_or_null("Main")
	if existing != null:
		existing.queue_free()
		await get_tree().process_frame
	root.add_child(instance)
	await get_tree().process_frame
	return instance

func _wait_for_screen(main: Node, expected_name: String, max_frames: int = 240) -> Node:
	var screen_root: Node = main.get_node("ScreenRoot")
	for _i in range(max_frames):
		var found := screen_root.get_node_or_null(expected_name)
		if found != null:
			return found
		await get_tree().process_frame
	return null

func test_phase2_officer_assign_then_save_load_then_verify_activity_feed() -> void:
	OS.set_environment("GD_GUILD_DB_PATH", "gdunit/phase2-officer-" + str(Time.get_ticks_usec()) + ".db")
	await _reset_root_node("GuildManager")
	await _ensure_root_node("res://Game.Godot/Scripts/Autoload/PlayerSession.cs", "PlayerSession")
	await _ensure_root_node("res://Game.Godot/Scripts/Autoload/GuildManager.cs", "GuildManager")
	await _ensure_root_node("res://Game.Godot/Adapters/DataStoreAdapter.cs", "DataStore")

	# Keep Activity Feed open during the flow (no historical replay guarantee).
	var activity := preload("res://Game.Godot/Scenes/Screens/ActivityFeedScreen.tscn").instantiate()
	activity.name = "ActivityFeedScreen"
	add_child(auto_free(activity))
	await get_tree().process_frame
	var feed: RichTextLabel = activity.get_node("Body/Scroll/Feed")

	var panel: Control = preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	panel.name = "GuildPanel"
	add_child(auto_free(panel))
	await get_tree().process_frame

	var start := preload("res://Game.Godot/Scenes/Screens/StartScreen.tscn").instantiate()
	start.name = "StartScreen"
	add_child(auto_free(start))
	await get_tree().process_frame

	var create_btn: Button = panel.get_node("Scroll/Margin/VBox/Actions/CreateGuildButton")
	var member_count: Label = panel.get_node("Scroll/Margin/VBox/GuildInfo/MemberCountLabel")
	var user_id: LineEdit = panel.get_node("Scroll/Margin/VBox/RosterActions/UserIdRow/UserIdInput")
	var join_btn: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/JoinButton")
	var status_label: Label = panel.get_node("Scroll/Margin/VBox/GuildInfo/StatusPanel/Root/Message")
	var guild_name: LineEdit = panel.get_node("Scroll/Margin/VBox/GuildInfo/GuildNameRow/GuildNameInput")

	guild_name.text = "OfficerGuild"
	create_btn.emit_signal("pressed")
	for _i in range(480):
		await get_tree().process_frame
		if member_count.text == "Members: 1":
			break
	assert_str(member_count.text).contains("Members: 1")

	user_id.text = "u2"
	join_btn.emit_signal("pressed")
	for _i in range(480):
		await get_tree().process_frame
		if member_count.text == "Members: 2":
			break
	assert_str(member_count.text).contains("Members: 2")

	# Assign officer via GuildManager (persist + publish event).
	var guild_manager := get_node_or_null("/root/GuildManager")
	assert_object(guild_manager).is_not_null()
	var summary_json = guild_manager.call("GetCurrentGuildSummaryJson")
	var parsed = JSON.parse_string(String(summary_json))
	assert_object(parsed).is_not_null()
	var guild_id := String(parsed["guildId"])

	var before_status := status_label.text
	guild_manager.call("AssignOfficer", guild_id, "u2", 0)
	for _i in range(480):
		await get_tree().process_frame
		if status_label.text != before_status and status_label.text.to_lower().find("officer assigned") >= 0:
			break
	assert_str(status_label.text.to_lower()).contains("officer assigned")

	# StartScreen Save+Load.
	var btn_save_load: Button = start.get_node("Center/VBox/BtnSaveLoad")
	btn_save_load.emit_signal("pressed")
	for _i in range(480):
		await get_tree().process_frame
		if feed.get_parsed_text().find("core.save.requested") >= 0 and feed.get_parsed_text().find("core.load.requested") >= 0 and feed.get_parsed_text().find("core.guild.officer.assigned") >= 0:
			break

	var text := feed.get_parsed_text()
	assert_str(text).contains("core.guild.officer.assigned")
	assert_str(text).contains("core.save.requested")
	assert_str(text).contains("core.load.requested")
