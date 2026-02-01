extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: Guild vertical slice.
## Create -> Member join -> Recruitment apply/approve -> UI updates via events.

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

func _item_list_contains(list: ItemList, prefix: String) -> bool:
	for i in range(list.item_count):
		if String(list.get_item_text(i)).begins_with(prefix):
			return true
	return false

func test_phase2_guild_vertical_slice_create_join_recruit_approve() -> void:
	# Use an isolated DB file for deterministic runs.
	OS.set_environment("GD_GUILD_DB_PATH", "gdunit/phase2-guild-" + str(Time.get_ticks_usec()) + ".db")
	await _reset_root_node("GuildManager")
	await _ensure_root_node("res://Game.Godot/Scripts/Autoload/PlayerSession.cs", "PlayerSession")
	await _ensure_root_node("res://Game.Godot/Scripts/Autoload/GuildManager.cs", "GuildManager")

	var main := await _spawn_main_on_root()
	var nav := main.get_node_or_null("ScreenNavigator")
	if nav != null and nav.has_method("set"):
		nav.UseFadeTransition = false

	var menu: Control = main.get_node("MainMenu")
	var btn_guild: Button = menu.get_node("VBox/BtnGuild")
	btn_guild.emit_signal("pressed")
	var guild_screen := await _wait_for_screen(main, "GuildScreen")
	assert_object(guild_screen).is_not_null()

	var panel: Control = guild_screen.get_node("Scroll/GuildPanel")
	var guild_name: LineEdit = panel.get_node("Scroll/Margin/VBox/GuildInfo/GuildNameRow/GuildNameInput")
	var create_btn: Button = panel.get_node("Scroll/Margin/VBox/Actions/CreateGuildButton")
	var member_count: Label = panel.get_node("Scroll/Margin/VBox/GuildInfo/MemberCountLabel")
	var members: ItemList = panel.get_node("Scroll/Margin/VBox/MembersListPanel/Root/Items")

	guild_name.text = "PlayGuild"
	create_btn.emit_signal("pressed")

	for _i in range(480):
		await get_tree().process_frame
		if member_count.text == "Members: 1":
			break
	assert_str(member_count.text).contains("Members: 1")
	assert_bool(_item_list_contains(members, "player1")).is_true()

	# Member join
	var user_id: LineEdit = panel.get_node("Scroll/Margin/VBox/RosterActions/UserIdRow/UserIdInput")
	var join_btn: Button = panel.get_node("Scroll/Margin/VBox/RosterActions/MemberActionsRow/JoinButton")
	user_id.text = "u2"
	join_btn.emit_signal("pressed")
	for _i in range(480):
		await get_tree().process_frame
		if member_count.text == "Members: 2" and _item_list_contains(members, "u2"):
			break
	assert_str(member_count.text).contains("Members: 2")
	assert_bool(_item_list_contains(members, "u2")).is_true()

	# Recruitment apply/approve
	var candidate_id: LineEdit = panel.get_node("Scroll/Margin/VBox/RecruitmentSection/CandidateIdRow/CandidateIdInput")
	var offer_id: LineEdit = panel.get_node("Scroll/Margin/VBox/RecruitmentSection/OfferIdRow/OfferIdInput")
	var apply_btn: Button = panel.get_node("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/ApplyButton")
	var approve_btn: Button = panel.get_node("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/ApproveButton")
	var offers: ItemList = panel.get_node("Scroll/Margin/VBox/RecruitmentSection/OffersList")

	candidate_id.text = "u3"
	apply_btn.emit_signal("pressed")

	var first_offer_text := ""
	for _i in range(480):
		await get_tree().process_frame
		if offers.item_count > 0:
			first_offer_text = offers.get_item_text(0)
			break
	assert_int(offers.item_count).is_greater_equal(1)

	var offer_parts := String(first_offer_text).split(" | ")
	assert_int(offer_parts.size()).is_greater_equal(1)
	offer_id.text = offer_parts[0]
	approve_btn.emit_signal("pressed")

	for _i in range(480):
		await get_tree().process_frame
		if offers.item_count == 0 and member_count.text == "Members: 3" and _item_list_contains(members, "u3"):
			break
	assert_int(offers.item_count).is_equal(0)
	assert_str(member_count.text).contains("Members: 3")
	assert_bool(_item_list_contains(members, "u3")).is_true()

