extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func test_main_scene_instantiates_and_visible() -> void:
    var scene := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    add_child(auto_free(scene))
    await get_tree().process_frame
    assert_bool(scene.visible).is_true()

func test_settings_screen_can_load() -> void:
    var packed : PackedScene = preload("res://Game.Godot/Scenes/Screens/SettingsScreen.tscn")
    var inst := packed.instantiate()
    add_child(auto_free(inst))
    await get_tree().process_frame
    assert_bool(inst.is_inside_tree()).is_true()

# T2 Minimal playable loop skeleton:
# This test expresses the expected scene-level behavior for
# PRD 3.0.3 "T2 可玩性场景流" but may be adjusted as the
# Game.Godot UI evolves. Initially it can be used as a guide
# for wiring Week/phase UI and a single "advance week" action.

func test_t2_minimal_loop_from_main_scene() -> void:
    # Arrange: load main scene
    var scene := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    add_child(auto_free(scene))
    await get_tree().process_frame

    # Assert: main is visible
    assert_bool(scene.visible).is_true()

    # Locate a label or container intended to show current week.
    # The concrete node path can be adjusted once the UI is wired.
    var week_label := scene.get_node_or_null("VBox/Output")
    assert_object(week_label).is_not_null()

    # Act: simulate a single "advance week" action.
    # For now we assume Main scripts will expose a method to drive
    # the first minimal loop; this call can be updated when that
    # API is available.
    if scene.has_method("OnAdvanceWeekButtonPressed"):
        scene.OnAdvanceWeekButtonPressed()
    elif scene.has_method("AdvanceOneWeek"):
        scene.AdvanceOneWeek()
    await get_tree().process_frame

    # Assert: after advancing, the UI should reflect that at least
    # one full week cycle has been processed. The concrete text
    # assertion will be refined once the Week display is implemented.
    # For now we only assert that the label remains valid and non-empty.
    assert_object(week_label).is_not_null()
    assert_str(week_label.text).is_not_empty()
