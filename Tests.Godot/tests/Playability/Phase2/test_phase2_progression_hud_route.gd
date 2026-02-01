extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

## Phase 2 playability: HUD reacts to progression-related domain events (XP/Achievements).

func before() -> void:
	OS.set_environment("SECURITY_TEST_MODE", "1")

func _hud() -> Node:
	var hud := preload("res://Game.Godot/Scenes/UI/HUD.tscn").instantiate()
	add_child(auto_free(hud))
	await get_tree().process_frame
	return hud

func test_hud_updates_on_experience_and_achievement_events() -> void:
	var hud := await _hud()
	var xp_label: Label = hud.get_node("TopBar/HBox/ExperienceLabel")
	var achievements_label: Label = hud.get_node("TopBar/HBox/AchievementsLabel")
	var before_xp := xp_label.text
	var before_ach := achievements_label.text

	var bus := get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()

	# ExperienceChanged payload shape is JSON in UI tests; only minimal keys are required for display updates.
	bus.PublishSimple("core.experience.changed", "core", '{"userId":"u1","delta":10,"total":10,"level":1,"changedAt":"2025-01-01T00:00:00Z"}')
	await get_tree().process_frame

	# Achievements are tracked by AchievementTracker and are triggered by selected domain event types.
	# Trigger one known achievement trigger event type deterministically.
	bus.PublishSimple("core.guild.created", "core", "{}")
	await get_tree().process_frame

	assert_str(xp_label.text).is_not_equal(before_xp)
	assert_str(achievements_label.text).is_not_equal(before_ach)
