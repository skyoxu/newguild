extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Officer UI smoke tests.
## Focus: UI reacts to officer-related domain events (Task 39).

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

# ACC:T39.1
func test_officer_assigned_event_updates_status_message() -> void:
	var panel := await _guild_panel()
	var status_label := _status_label(panel)
	var before_text := status_label.text

	# Prime panel with a guild id via a guild created event
	var create_event := '{"guildId":"g-officer","creatorId":"u1","guildName":"OfficerGuild","createdAt":"2025-01-01T00:00:00Z"}'
	_bus.PublishSimple("core.guild.created", "GuildManager", create_event)
	await get_tree().process_frame

	# Publish officer assigned event
	var assigned_event := '{"guildId":"g-officer","userId":"u2","slot":"council"}'
	_bus.PublishSimple("core.guild.officer.assigned", "GuildManager", assigned_event)
	await get_tree().process_frame

	assert_str(status_label.text).is_not_equal(before_text)
	assert_str(status_label.text.to_lower()).contains("officer assigned")

