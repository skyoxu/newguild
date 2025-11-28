extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func test_t2_playable_loop_scene_skeleton() -> void:
    var scene := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    add_child(auto_free(scene))
    await get_tree().process_frame
    assert_bool(scene.visible).is_true()

