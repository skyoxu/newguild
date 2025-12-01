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

	assert_object(guild_name_label).is_not_null()
	assert_object(member_count_label).is_not_null()
	assert_object(members_list).is_not_null()
	assert_object(create_button).is_not_null()
	assert_object(disband_button).is_not_null()

func test_guild_panel_initial_state() -> void:
	var scene := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame

	var guild_name_label: Label = scene.get_node("VBox/GuildInfo/GuildNameLabel")
	var member_count_label: Label = scene.get_node("VBox/GuildInfo/MemberCountLabel")
	var members_list: ItemList = scene.get_node("VBox/MembersList")
	var create_button: Button = scene.get_node("VBox/Actions/CreateGuildButton")
	var disband_button: Button = scene.get_node("VBox/Actions/DisbandGuildButton")

	# Initial state: no guild
	assert_str(guild_name_label.text).is_equal("Guild: None")
	assert_str(member_count_label.text).is_equal("Members: 0")
	assert_int(members_list.item_count).is_equal(0)
	assert_bool(create_button.disabled).is_false()
	assert_bool(disband_button.disabled).is_true()
