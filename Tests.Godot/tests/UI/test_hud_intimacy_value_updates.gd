extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node

func before() -> void:
	_bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	_bus.name = "EventBus"
	get_tree().get_root().add_child(auto_free(_bus))


# ACC:T18.3
func test_intimacy_panel_updates_on_relationship_changed_event() -> void:
	var panel := preload("res://Game.Godot/Scenes/UI/IntimacyPanel.tscn").instantiate()
	add_child_autofree(panel)
	await get_tree().process_frame

	var label: Label = panel.get_node("IntimacyValueLabel")
	assert_str(label.text).contains("-")

	var evt := "{\"guildId\":\"g1\",\"subjectId\":\"m1\",\"otherId\":\"m2\",\"oldValue\":0,\"newValue\":42,\"changedAt\":\"2026-01-01T00:00:00Z\"}"
	_bus.PublishSimple("core.social.relationship.changed", "ut", evt)
	await get_tree().process_frame

	assert_str(label.text).contains("42")
