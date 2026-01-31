extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const GUILD_PANEL_SCENE := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn")
const EVENT_BUS_ADAPTER := preload("res://Game.Godot/Adapters/EventBusAdapter.cs")

# ContractRefs (Task 39):
# - core.guild.created
# - core.guild.officer.assigned
# - core.guild.officer.revoked

const EVT_GUILD_CREATED := "core.guild.created"
const EVT_GUILD_OFFICER_ASSIGNED := "core.guild.officer.assigned"
const EVT_GUILD_OFFICER_REVOKED := "core.guild.officer.revoked"
const EVT_GUILD_MEMBER_ROLE_CHANGED := "core.guild.member.role_changed"

var _bus: Node
var _previous_bus: Node
var _previous_bus_original_name: String
var _previous_bus_original_process_mode: Node.ProcessMode

func before() -> void:
	_bus = EVENT_BUS_ADAPTER.new()
	_bus.name = "EventBus"

	_previous_bus = get_node_or_null("/root/EventBus")
	if _previous_bus != null:
		_previous_bus_original_name = _previous_bus.name
		_previous_bus_original_process_mode = _previous_bus.process_mode
		_previous_bus.process_mode = Node.ProcessMode.PROCESS_MODE_DISABLED
		_previous_bus.name = "_EventBusBackup_%s" % str(_previous_bus.get_instance_id())

	get_tree().get_root().add_child(auto_free(_bus))

func after() -> void:
	if _bus != null and is_instance_valid(_bus):
		_bus.name = "_EventBusTemp"
	_bus = null

	if _previous_bus != null and is_instance_valid(_previous_bus):
		_previous_bus.name = _previous_bus_original_name
		_previous_bus.process_mode = _previous_bus_original_process_mode
	_previous_bus = null
	_previous_bus_original_name = ""
	_previous_bus_original_process_mode = Node.ProcessMode.PROCESS_MODE_INHERIT

func after_test() -> void:
	# Allow queued frees from auto_free() to be processed before orphan detection.
	await get_tree().process_frame
	await get_tree().process_frame

func _instantiate_guild_panel() -> Control:
	var panel := GUILD_PANEL_SCENE.instantiate()
	add_child(auto_free(panel))
	return panel

func _officers_section(panel: Node) -> Node:
	return panel.get_node_or_null("Scroll/Margin/VBox/OfficersSection")

func _emit_domain_event(type: String, data_json: String) -> void:
	assert_object(_bus).is_not_null()
	assert_bool(_bus.has_signal("DomainEventEmitted")).is_true()

	var source := "Task39GdUnit"
	var id := "t39"
	var spec_version := "1.0"
	var data_content_type := "application/json"
	var timestamp_iso := "2026-01-01T00:00:00Z"

	_bus.emit_signal(
		"DomainEventEmitted",
		type,
		source,
		data_json,
		id,
		spec_version,
		data_content_type,
		timestamp_iso
	)

func _find_officer_item_index(officers_list: ItemList, slot_label: String) -> int:
	var wanted := "%s:" % slot_label.strip_edges().to_lower()
	for i in range(officers_list.get_item_count()):
		var text := officers_list.get_item_text(i)
		if str(text).to_lower().begins_with(wanted):
			return i
	return -1

# ACC:T39.1
func test_officers_section_exists() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()

# ACC:T39.2
func test_officers_section_has_assign_button() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()
	assert_object(section.get_node_or_null("Actions/AssignOfficerButton")).is_not_null()

# ACC:T39.3
func test_officers_section_has_revoke_button() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()
	assert_object(section.get_node_or_null("Actions/RevokeOfficerButton")).is_not_null()

# ACC:T39.5
# ACC:T39.6
func test_domain_events_assign_then_revoke_update_status_and_list() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()
	var status_label := section.get_node_or_null("OfficerStatusLabel") as Label
	assert_object(status_label).is_not_null()

	var officers_list := section.get_node_or_null("OfficersList") as ItemList
	assert_object(officers_list).is_not_null()

	await get_tree().process_frame

	_emit_domain_event(
		EVT_GUILD_CREATED,
		"{\"guildId\":\"g1\",\"creatorId\":\"u-admin\",\"guildName\":\"Officers\"}"
	)
	await get_tree().process_frame

	var commander_index := _find_officer_item_index(officers_list, "commander")
	assert_int(commander_index).is_greater_equal(0)

	assert_str(status_label.text).contains("ready")
	assert_str(officers_list.get_item_text(commander_index)).contains("(unassigned)")

	_emit_domain_event(
		EVT_GUILD_OFFICER_ASSIGNED,
		"{\"guildId\":\"g1\",\"userId\":\"u1\",\"slot\":\"commander\"}"
	)
	await get_tree().process_frame

	assert_str(status_label.text).contains("Officer assigned")
	assert_str(officers_list.get_item_text(commander_index)).contains("u1")

	_emit_domain_event(
		EVT_GUILD_OFFICER_REVOKED,
		"{\"guildId\":\"g1\",\"userId\":\"u1\",\"slot\":\"commander\"}"
	)
	await get_tree().process_frame

	assert_str(status_label.text).contains("Officer revoked")
	assert_str(officers_list.get_item_text(commander_index)).contains("(unassigned)")

func _members_list(panel: Node) -> ItemList:
	return panel.get_node_or_null("Scroll/Margin/VBox/MembersListPanel/Root/Items") as ItemList

# ContractRefs coverage: core.guild.member.role_changed
func test_domain_event_member_role_changed_updates_roster_item() -> void:
	var panel := _instantiate_guild_panel()

	await get_tree().process_frame

	var members_list := _members_list(panel)
	assert_object(members_list).is_not_null()

	_emit_domain_event(
		EVT_GUILD_CREATED,
		"{\"guildId\":\"g1\",\"creatorId\":\"u-admin\",\"guildName\":\"Officers\"}"
	)
	await get_tree().process_frame

	assert_int(members_list.get_item_count()).is_greater(0)
	var before := members_list.get_item_text(0)
	assert_str(before).contains("u-admin")
	assert_str(before).contains("(Admin)")

	_emit_domain_event(
		EVT_GUILD_MEMBER_ROLE_CHANGED,
		"{\"guildId\":\"g1\",\"userId\":\"u-admin\",\"oldRole\":\"admin\",\"newRole\":\"member\",\"changedAt\":\"2026-01-01T00:00:00Z\",\"changedByUserId\":\"u-admin\"}"
	)
	await get_tree().process_frame

	var after := members_list.get_item_text(0)
	assert_str(after).contains("u-admin")
	assert_str(after).contains("(Member)")

# ACC:T39.6
func test_section_has_slot_and_user_inputs() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()
	assert_object(section.get_node_or_null("OfficerSlotOption")).is_not_null()
	assert_object(section.get_node_or_null("OfficerUserIdInput")).is_not_null()

# ACC:T39.7
func test_buttons_are_clickable() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()
	var assign_button := section.get_node_or_null("Actions/AssignOfficerButton") as Button
	var revoke_button := section.get_node_or_null("Actions/RevokeOfficerButton") as Button
	assert_object(assign_button).is_not_null()
	assert_object(revoke_button).is_not_null()
	assert_bool(assign_button.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()
	assert_bool(revoke_button.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()
