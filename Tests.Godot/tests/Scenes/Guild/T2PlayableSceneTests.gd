extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func after_test() -> void:
    # Allow queued frees from auto_free() to be processed before orphan detection.
    await get_tree().process_frame
    await get_tree().process_frame

# ACC:T27.6
# ACC:T27.7
func test_t2_playable_loop_scene_skeleton() -> void:
    var scene := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    add_child(scene)
    auto_free(scene)
    await get_tree().process_frame
    assert_bool(scene.visible).is_true()
