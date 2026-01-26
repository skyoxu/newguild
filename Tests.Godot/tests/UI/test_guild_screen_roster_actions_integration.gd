extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Integration test: instantiate the real screen and drive roster UI buttons.
## Validates the end-to-end chain:
## UI button -> GuildManager (adapter) -> GuildRosterService (core) -> EventBusAdapter -> UI list updates.

var _bus: Node
var _guild_manager: Node
var _session
var _logger

func _new_csharp_node(script_path: String, node_name: String) -> Node:
	var n := Node.new()
	n.set_script(load(script_path))
	n.name = node_name
	return n

func before() -> void:
	var suffix := str(Time.get_unix_time_from_system()) + "-" + str(randi() % 1000000)
	OS.set_environment("GD_GUILD_DB_PATH", "data/gdunit-guild-" + suffix + ".db")

	_logger = _new_csharp_node("res://Game.Godot/Adapters/LoggerAdapter.cs", "Logger")
	get_tree().get_root().add_child(auto_free(_logger))

	_bus = get_node_or_null("/root/EventBus")
	assert_object(_bus).is_not_null()

	_session = _new_csharp_node("res://Game.Godot/Scripts/Autoload/PlayerSession.cs", "PlayerSession")
	get_tree().get_root().add_child(auto_free(_session))

	_guild_manager = _new_csharp_node("res://Game.Godot/Scripts/Autoload/GuildManager.cs", "GuildManager")
	get_tree().get_root().add_child(auto_free(_guild_manager))

	await get_tree().process_frame

func _guild_screen() -> Node:
	var screen := preload("res://Game.Godot/Scenes/Screens/GuildScreen.tscn").instantiate()
	add_child(auto_free(screen))
	await get_tree().process_frame
	return screen

func _await_until(pred: Callable, frames: int = 180) -> void:
	for _i in range(frames):
		if pred.call():
			return
		await get_tree().process_frame
	assert_bool(pred.call()).is_true()

func _list_contains(members_list: ItemList, user_id: String) -> bool:
	for i in range(members_list.item_count):
		if members_list.get_item_text(i).begins_with(user_id):
			return true
	return false

func _item_text(members_list: ItemList, user_id: String) -> String:
	for i in range(members_list.item_count):
		if members_list.get_item_text(i).begins_with(user_id):
			return members_list.get_item_text(i)
	return ""

# ACC:T13.3
func test_roster_actions_update_member_list_via_events() -> void:
	var screen := await _guild_screen()
	var panel: Node = screen.get_node("Scroll/GuildPanel")

	var create_button: Button = panel.get_node("Scroll/Margin/VBox/Actions/CreateGuildButton")
	var members_list: ItemList = panel.get_node("Scroll/Margin/VBox/MembersListPanel/Root/Items")
	var user_id_input: LineEdit = panel.get_node("Scroll/Margin/VBox/RosterActions/UserIdRow/UserIdInput")
	var join_button: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/JoinButton")
	var leave_button: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/LeaveButton")
	var promote_button: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/PromoteButton")
	var demote_button: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/DemoteButton")
	var kick_button: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/KickButton")

	# Create a guild (creator is player1)
	create_button.pressed.emit()
	await _await_until(func() -> bool: return members_list.item_count == 1 and create_button.disabled)
	assert_str(members_list.get_item_text(0)).contains("player1")

	# Join u2
	user_id_input.text = "u2"
	join_button.pressed.emit()
	await _await_until(func() -> bool: return members_list.item_count == 2 and _list_contains(members_list, "u2"))
	assert_str(_item_text(members_list, "u2")).contains("Member")

	# Promote u2 -> Admin
	promote_button.pressed.emit()
	await _await_until(func() -> bool: return _item_text(members_list, "u2").contains("Admin"))

	# Demote u2 -> Member
	demote_button.pressed.emit()
	await _await_until(func() -> bool: return _item_text(members_list, "u2").contains("Member"))

	# u2 leaves by switching the local session user
	_session.call("SetCurrentUserId", "u2")
	leave_button.pressed.emit()
	await _await_until(func() -> bool: return members_list.item_count == 1 and not _list_contains(members_list, "u2"))

	# Join u3 and then kick u3
	_session.call("SetCurrentUserId", "player1")
	user_id_input.text = "u3"
	join_button.pressed.emit()
	await _await_until(func() -> bool: return members_list.item_count == 2 and _list_contains(members_list, "u3"))

	kick_button.pressed.emit()
	await _await_until(func() -> bool: return members_list.item_count == 1 and not _list_contains(members_list, "u3"))
