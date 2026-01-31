extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const GUILD_PANEL_SCENE := preload("res://Game.Godot/Scenes/UI/GuildPanel.tscn")

# ContractRefs (Task 39):
# - core.guild.officer.assigned
# - core.guild.officer.revoked

func _instantiate_guild_panel() -> Control:
	var panel := GUILD_PANEL_SCENE.instantiate()
	add_child(auto_free(panel))
	return panel

func _officers_section(panel: Node) -> Node:
	return panel.get_node_or_null("Scroll/Margin/VBox/OfficersSection")

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
func test_assign_then_revoke_updates_status_label() -> void:
	var panel := _instantiate_guild_panel()
	var section := _officers_section(panel)
	assert_object(section).is_not_null()
	var status_label := section.get_node_or_null("OfficerStatusLabel") as Label
	assert_object(status_label).is_not_null()

	var slot_option := section.get_node_or_null("OfficerSlotOption") as OptionButton
	var user_id_input := section.get_node_or_null("OfficerUserIdInput") as LineEdit
	var assign_button := section.get_node_or_null("Actions/AssignOfficerButton") as Button
	var revoke_button := section.get_node_or_null("Actions/RevokeOfficerButton") as Button
	assert_object(slot_option).is_not_null()
	assert_object(user_id_input).is_not_null()
	assert_object(assign_button).is_not_null()
	assert_object(revoke_button).is_not_null()

	assert_int(slot_option.get_item_count()).is_greater(0)
	user_id_input.text = "u1"

	var initial := status_label.text
	assign_button.emit_signal("pressed")
	assert_str(status_label.text).is_not_equal(initial)

	var after_assign := status_label.text
	revoke_button.emit_signal("pressed")
	assert_str(status_label.text).is_not_equal(after_assign)

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
