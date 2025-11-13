extends "res://addons/gdUnit4/src/core/GdUnitTestSuite.gd"

var _loader: Node

func before() -> void:
    _loader = load("res://Game.Godot/Adapters/ResourceLoaderAdapter.cs").new()
    add_child_autofree(_loader)

func after() -> void:
    _loader = null

func test_load_text_from_res_should_succeed() -> void:
    var txt := _loader.LoadText("res://project.godot")
    assert_that(txt).is_not_null()
    assert_str(txt).contains("[application]")

func test_load_text_absolute_path_should_fail() -> void:
    var txt := _loader.LoadText("C:/Windows/System32/drivers/etc/hosts")
    assert_that(txt).is_null()

func test_load_text_with_parent_traversal_should_fail() -> void:
    var txt := _loader.LoadText("res://../project.godot")
    assert_that(txt).is_null()

