extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Test: GuildPanel scene instantiation and basic structure
## Verifies that GuildPanel scene loads correctly with all required nodes

func test_guild_panel_scene_instantiates() -> void:
	var scene := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame
	assert_bool(scene.visible).is_true()

func test_guild_panel_has_required_nodes() -> void:
	var scene := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame

	# Verify UI structure
	var guild_name_label: Label = scene.get_node("VBox/GuildInfo/GuildNameLabel")
	var member_count_label: Label = scene.get_node("VBox/GuildInfo/MemberCountLabel")
	var members_list: ItemList = scene.get_node("VBox/MembersList")
	var create_button: Button = scene.get_node("VBox/Actions/CreateGuildButton")
	var disband_button: Button = scene.get_node("VBox/Actions/DisbandGuildButton")
	var user_id_input: LineEdit = scene.get_node("VBox/RosterActions/UserIdRow/UserIdInput")
	var join_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/JoinButton")
	var leave_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/LeaveButton")
	var promote_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/PromoteButton")
	var demote_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/DemoteButton")
	var kick_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/KickButton")

	assert_object(guild_name_label).is_not_null()
	assert_object(member_count_label).is_not_null()
	assert_object(members_list).is_not_null()
	assert_object(create_button).is_not_null()
	assert_object(disband_button).is_not_null()
	assert_object(user_id_input).is_not_null()
	assert_object(join_button).is_not_null()
	assert_object(leave_button).is_not_null()
	assert_object(promote_button).is_not_null()
	assert_object(demote_button).is_not_null()
	assert_object(kick_button).is_not_null()

func test_guild_panel_initial_state() -> void:
	var scene := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame

	var guild_name_label: Label = scene.get_node("VBox/GuildInfo/GuildNameLabel")
	var member_count_label: Label = scene.get_node("VBox/GuildInfo/MemberCountLabel")
	var members_list: ItemList = scene.get_node("VBox/MembersList")
	var create_button: Button = scene.get_node("VBox/Actions/CreateGuildButton")
	var disband_button: Button = scene.get_node("VBox/Actions/DisbandGuildButton")
	var join_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/JoinButton")
	var leave_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/LeaveButton")
	var promote_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/PromoteButton")
	var demote_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/DemoteButton")
	var kick_button: Button = scene.get_node("VBox/RosterActions/MemberActionsRow/KickButton")

	# Initial state: no guild
	assert_str(guild_name_label.text).is_equal("Guild: None")
	assert_str(member_count_label.text).is_equal("Members: 0")
	assert_int(members_list.item_count).is_equal(0)
	assert_bool(create_button.disabled).is_false()
	assert_bool(disband_button.disabled).is_true()
	assert_bool(join_button.disabled).is_true()
	assert_bool(leave_button.disabled).is_true()
	assert_bool(promote_button.disabled).is_true()
	assert_bool(demote_button.disabled).is_true()
	assert_bool(kick_button.disabled).is_true()
