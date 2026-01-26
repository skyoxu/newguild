extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Test: GuildPanel scene instantiation and basic structure
## Verifies that GuildPanel scene loads correctly with all required nodes

# ACC:T31.2
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
	var vbox: VBoxContainer = scene.get_node_or_null("Scroll/Margin/VBox")
	assert_object(vbox).is_not_null()
	if vbox == null:
		return

	var guild_name_input: LineEdit = vbox.get_node_or_null("GuildInfo/GuildNameRow/GuildNameInput")
	var status_panel: Control = vbox.get_node_or_null("GuildInfo/StatusPanel")
	var error_panel: Control = vbox.get_node_or_null("GuildInfo/ErrorPanel")
	var confirm_dialog: Control = vbox.get_node_or_null("GuildInfo/ConfirmDisbandDialog")
	var member_count_label: Label = vbox.get_node_or_null("GuildInfo/MemberCountLabel")
	var members_list_panel: Control = vbox.get_node_or_null("MembersListPanel")
	var members_list: ItemList = vbox.get_node_or_null("MembersListPanel/Root/Items")
	var create_button: Button = vbox.get_node_or_null("Actions/CreateGuildButton")
	var disband_button: Button = vbox.get_node_or_null("Actions/DisbandGuildButton")
	var user_id_input: LineEdit = vbox.get_node_or_null("RosterActions/UserIdRow/UserIdInput")
	var join_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/JoinButton")
	var leave_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/LeaveButton")
	var promote_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/PromoteButton")
	var demote_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/DemoteButton")
	var kick_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/KickButton")

	assert_object(guild_name_input).is_not_null()
	assert_object(status_panel).is_not_null()
	assert_object(error_panel).is_not_null()
	assert_object(confirm_dialog).is_not_null()
	assert_object(member_count_label).is_not_null()
	assert_object(members_list_panel).is_not_null()
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

	var vbox: VBoxContainer = scene.get_node_or_null("Scroll/Margin/VBox")
	assert_object(vbox).is_not_null()
	if vbox == null:
		return

	var guild_name_input: LineEdit = vbox.get_node_or_null("GuildInfo/GuildNameRow/GuildNameInput")
	var status_panel: Control = vbox.get_node_or_null("GuildInfo/StatusPanel")
	var member_count_label: Label = vbox.get_node_or_null("GuildInfo/MemberCountLabel")
	var members_list: ItemList = vbox.get_node_or_null("MembersListPanel/Root/Items")
	var create_button: Button = vbox.get_node_or_null("Actions/CreateGuildButton")
	var disband_button: Button = vbox.get_node_or_null("Actions/DisbandGuildButton")
	var join_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/JoinButton")
	var leave_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/LeaveButton")
	var promote_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/PromoteButton")
	var demote_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/DemoteButton")
	var kick_button: Button = vbox.get_node_or_null("RosterActions/MemberActionsRow/KickButton")

	# Initial state: no guild
	assert_object(guild_name_input).is_not_null()
	assert_object(status_panel).is_not_null()
	assert_object(member_count_label).is_not_null()
	assert_object(members_list).is_not_null()
	assert_object(create_button).is_not_null()
	assert_object(disband_button).is_not_null()
	assert_object(join_button).is_not_null()
	assert_object(leave_button).is_not_null()
	assert_object(promote_button).is_not_null()
	assert_object(demote_button).is_not_null()
	assert_object(kick_button).is_not_null()

	if guild_name_input == null or status_panel == null or member_count_label == null or members_list == null or create_button == null or disband_button == null or join_button == null or leave_button == null or promote_button == null or demote_button == null or kick_button == null:
		return

	var status_message: Label = status_panel.get_node_or_null("Root/Message")
	assert_object(status_message).is_not_null()
	if status_message == null:
		return
	assert_str(status_message.text).is_equal("")
	assert_str(guild_name_input.text).is_equal("")
	assert_bool(guild_name_input.editable).is_true()
	assert_str(member_count_label.text).is_equal("Members: 0")
	assert_int(members_list.item_count).is_equal(0)
	assert_bool(create_button.disabled).is_false()
	assert_bool(disband_button.disabled).is_true()
	assert_bool(join_button.disabled).is_true()
	assert_bool(leave_button.disabled).is_true()
	assert_bool(promote_button.disabled).is_true()
	assert_bool(demote_button.disabled).is_true()
	assert_bool(kick_button.disabled).is_true()


# ACC:T31.4
func test_guild_panel_buttons_have_interactable_flags_by_default() -> void:
	var scene := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame

	var vbox: VBoxContainer = scene.get_node_or_null("Scroll/Margin/VBox")
	assert_object(vbox).is_not_null()
	if vbox == null:
		return

	var paths := [
		"Actions/CreateGuildButton",
		"Actions/DisbandGuildButton",
		"RosterActions/MemberActionsRow/JoinButton",
		"RosterActions/MemberActionsRow/LeaveButton",
		"RosterActions/MemberActionsRow/PromoteButton",
		"RosterActions/MemberActionsRow/DemoteButton",
		"RosterActions/MemberActionsRow/KickButton",
	]

	for rel_path in paths:
		var btn: Button = vbox.get_node_or_null(rel_path)
		assert_object(btn).is_not_null()
		if btn == null:
			continue
		assert_bool(btn.visible).is_true()
		assert_bool(btn.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()

	var create_button: Button = vbox.get_node_or_null("Actions/CreateGuildButton")
	assert_object(create_button).is_not_null()
	if create_button == null:
		return

	var connections := create_button.get_signal_connection_list("pressed")
	assert_int(connections.size()).is_greater(0)
