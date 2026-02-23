extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE := "res://Game.Godot/Scenes/UI/HUD.tscn"
const EVENT_BUS_SCRIPT := "res://Game.Godot/Adapters/EventBusAdapter.cs"
const ACHIEVEMENT_STATE_KEY := "achievement_state_t2-demo"

func _ensure_event_bus(root: Node) -> Node:
	var existing := root.get_node_or_null("EventBus")
	if existing != null and existing.has_method("PublishSimple"):
		return existing

	if existing != null:
		existing.queue_free()
		await get_tree().process_frame

	var script := load(EVENT_BUS_SCRIPT)
	assert_object(script).is_not_null()
	var bus := script.new()
	bus.name = "EventBus"
	root.add_child(auto_free(bus))
	await get_tree().process_frame
	return bus

func _reset_achievement_state() -> void:
	var data_store := get_tree().get_root().get_node_or_null("DataStore")
	if data_store != null and data_store.has_method("DeleteSync"):
		data_store.call("DeleteSync", ACHIEVEMENT_STATE_KEY)

# ACC:T36.1 ACC:T50.1 ACC:T50.4
func test_hud_achievements_label_updates_on_set() -> void:
	_reset_achievement_state()
	var root := get_tree().get_root()
	var bus := await _ensure_event_bus(root)

	var scene := preload(HUD_SCENE).instantiate()
	root.add_child(auto_free(scene))
	await get_tree().process_frame
	var can_publish := bus.has_method("PublishSimple")
	if can_publish:
		bus.call("PublishSimple", "core.guild.created", "test", "{\"guildId\":\"g1\"}")
	await get_tree().process_frame
	var label := scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_bool(can_publish).is_true()
	assert_object(label).is_not_null()
	if label != null:
		assert_str(label.text).contains("Achievements")
		assert_str(label.text).contains("1")
	_reset_achievement_state()

# ACC:T50.4
func test_hud_achievements_persists_after_scene_recreate() -> void:
	_reset_achievement_state()
	var root := get_tree().get_root()
	var bus := await _ensure_event_bus(root)

	var first_scene := preload(HUD_SCENE).instantiate()
	root.add_child(auto_free(first_scene))
	await get_tree().process_frame
	bus.call("PublishSimple", "core.guild.created", "test", "{\"guildId\":\"g1\"}")
	await get_tree().process_frame

	var first_label := first_scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(first_label).is_not_null()
	if first_label != null:
		assert_str(first_label.text).contains("1")

	first_scene.queue_free()
	await get_tree().process_frame

	var second_scene := preload(HUD_SCENE).instantiate()
	root.add_child(auto_free(second_scene))
	await get_tree().process_frame
	var second_label := second_scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(second_label).is_not_null()
	if second_label != null:
		assert_str(second_label.text).contains("1")

	_reset_achievement_state()

# ACC:T36.3 ACC:T50.7
func test_hud_achievements_label_default_is_zero() -> void:
	var scene := preload(HUD_SCENE).instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame
	var label := scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(label).is_not_null()
	if label != null:
		assert_str(label.text).contains("0")
