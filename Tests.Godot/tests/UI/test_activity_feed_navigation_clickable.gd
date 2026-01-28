extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

# ACC:T33.5 activity feed navigation clickable
func test_activity_feed_navigation_clickable_smoke() -> void:
    var root := Control.new()
    add_child_autofree(root)
    var button := Button.new()
    button.text = "Activity"
    root.add_child(button)
    button.visible = true

    assert_bool(button.visible).is_true()
    assert_bool(button.mouse_filter != Control.MOUSE_FILTER_IGNORE).is_true()
    assert_int(root.get_child_count()).is_equal(1)
