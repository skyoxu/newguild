extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Test: GuildPanel event integration
## Verifies that GuildPanel correctly responds to domain events from EventBusAdapter

var _bus: Node

func before() -> void:
	_bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	_bus.name = "EventBus"
	get_tree().get_root().add_child(auto_free(_bus))

func _guild_panel() -> Node:
	var panel = preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn").instantiate()
	add_child(auto_free(panel))
	await get_tree().process_frame
	return panel

func test_guild_panel_updates_on_guild_created_event() -> void:
	var panel = await _guild_panel()
	var guild_name_label: Label = panel.get_node("VBox/GuildInfo/GuildNameLabel")
	var member_count_label: Label = panel.get_node("VBox/GuildInfo/MemberCountLabel")
	var members_list: ItemList = panel.get_node("VBox/MembersList")
	var create_button: Button = panel.get_node("VBox/Actions/CreateGuildButton")
	var disband_button: Button = panel.get_node("VBox/Actions/DisbandGuildButton")

	# Publish guild created event
	var event_data = '{"guildId":"g123","creatorId":"u456","guildName":"TestGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", event_data)
	await get_tree().process_frame

	# Verify UI updates
	assert_str(guild_name_label.text).contains("TestGuild")
	assert_str(member_count_label.text).contains("1")
	assert_int(members_list.item_count).is_equal(1)
	assert_str(members_list.get_item_text(0)).contains("u456")
	assert_str(members_list.get_item_text(0)).contains("Admin")
	assert_bool(create_button.disabled).is_true()
	assert_bool(disband_button.disabled).is_false()

func test_guild_panel_updates_on_member_joined_event() -> void:
	var panel = await _guild_panel()
	var members_list: ItemList = panel.get_node("VBox/MembersList")
	var member_count_label: Label = panel.get_node("VBox/GuildInfo/MemberCountLabel")

	# First create a guild
	var create_event = '{"guildId":"g123","creatorId":"u1","guildName":"MyGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", create_event)
	await get_tree().process_frame

	# Then add a member
	var join_event = '{"userId":"u2","guildId":"g123","joinedAt":"2025-01-01T01:00:00Z","role":"Member"}'
	_bus.PublishSimple("core.guild.member.joined", "GuildManager", join_event)
	await get_tree().process_frame

	# Verify member added
	assert_int(members_list.item_count).is_equal(2)
	assert_str(member_count_label.text).contains("2")
	assert_str(members_list.get_item_text(1)).contains("u2")
	assert_str(members_list.get_item_text(1)).contains("Member")

func test_guild_panel_updates_on_member_left_event() -> void:
	var panel = await _guild_panel()
	var members_list: ItemList = panel.get_node("VBox/MembersList")
	var member_count_label: Label = panel.get_node("VBox/GuildInfo/MemberCountLabel")

	# Create guild with creator
	var create_event = '{"guildId":"g123","creatorId":"u1","guildName":"MyGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", create_event)
	await get_tree().process_frame

	# Add a member
	var join_event = '{"userId":"u2","guildId":"g123","joinedAt":"2025-01-01T01:00:00Z","role":"Member"}'
	_bus.PublishSimple("core.guild.member.joined", "GuildManager", join_event)
	await get_tree().process_frame

	assert_int(members_list.item_count).is_equal(2)

	# Member leaves
	var left_event = '{"userId":"u2","guildId":"g123","leftAt":"2025-01-01T02:00:00Z","reason":"Voluntary"}'
	_bus.PublishSimple("core.guild.member.left", "GuildManager", left_event)
	await get_tree().process_frame

	# Verify member removed
	assert_int(members_list.item_count).is_equal(1)
	assert_str(member_count_label.text).contains("1")
	assert_str(members_list.get_item_text(0)).contains("u1")

func test_guild_panel_updates_on_member_role_changed_event() -> void:
	var panel = await _guild_panel()
	var members_list: ItemList = panel.get_node("VBox/MembersList")

	# Create guild
	var create_event = '{"guildId":"g123","creatorId":"u1","guildName":"MyGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", create_event)
	await get_tree().process_frame

	# Add member
	var join_event = '{"userId":"u2","guildId":"g123","joinedAt":"2025-01-01T01:00:00Z","role":"Member"}'
	_bus.PublishSimple("core.guild.member.joined", "GuildManager", join_event)
	await get_tree().process_frame

	assert_str(members_list.get_item_text(1)).contains("Member")

	# Promote member
	var role_event = '{"userId":"u2","guildId":"g123","oldRole":"Member","newRole":"Admin","changedAt":"2025-01-01T02:00:00Z","changedByUserId":"u1"}'
	_bus.PublishSimple("core.guild.member.role_changed", "GuildManager", role_event)
	await get_tree().process_frame

	# Verify role updated
	assert_str(members_list.get_item_text(1)).contains("Admin")
	assert_str(members_list.get_item_text(1)).does_not_contain("Member")

func test_guild_panel_updates_on_guild_disbanded_event() -> void:
	var panel = await _guild_panel()
	var guild_name_label: Label = panel.get_node("VBox/GuildInfo/GuildNameLabel")
	var member_count_label: Label = panel.get_node("VBox/GuildInfo/MemberCountLabel")
	var members_list: ItemList = panel.get_node("VBox/MembersList")
	var create_button: Button = panel.get_node("VBox/Actions/CreateGuildButton")
	var disband_button: Button = panel.get_node("VBox/Actions/DisbandGuildButton")

	# Create guild first
	var create_event = '{"guildId":"g123","creatorId":"u1","guildName":"MyGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", create_event)
	await get_tree().process_frame

	assert_bool(create_button.disabled).is_true()

	# Disband guild
	var disband_event = '{"guildId":"g123","disbandedByUserId":"u1","disbandedAt":"2025-01-01T03:00:00Z","reason":"Test"}'
	_bus.PublishSimple("core.guild.disbanded", "GuildManager", disband_event)
	await get_tree().process_frame

	# Verify UI reset
	assert_str(guild_name_label.text).is_equal("Guild: None")
	assert_str(member_count_label.text).is_equal("Members: 0")
	assert_int(members_list.item_count).is_equal(0)
	assert_bool(create_button.disabled).is_false()
	assert_bool(disband_button.disabled).is_true()
