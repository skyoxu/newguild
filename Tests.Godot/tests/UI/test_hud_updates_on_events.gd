extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node

func before() -> void:
    _bus = get_node_or_null("/root/EventBus")
    assert_object(_bus).is_not_null()

func _hud() -> Node:
    var hud = preload("res://Game.Godot/Scenes/UI/HUD.tscn").instantiate()
    add_child(auto_free(hud))
    await get_tree().process_frame
    return hud

func test_hud_updates_on_score_event() -> void:
    var hud = await _hud()
    var score_label: Label = hud.get_node("TopBar/HBox/ScoreLabel")
    _bus.PublishSimple("core.score.changed", "ut", "{\"value\":42}")
    await get_tree().process_frame
    assert_str(score_label.text).contains("42")

func test_hud_updates_on_health_event() -> void:
    var hud = await _hud()
    var hp_label: Label = hud.get_node("TopBar/HBox/HealthLabel")
    _bus.PublishSimple("core.health.updated", "ut", "{\"value\":77}")
    await get_tree().process_frame
    assert_str(hp_label.text).contains("77")

func test_hud_updates_on_experience_event() -> void:
    var hud = await _hud()
    var xp_label: Label = hud.get_node("TopBar/HBox/ExperienceLabel")
    _bus.PublishSimple(
        "core.experience.changed",
        "ut",
        "{\"guildId\":\"g1\",\"totalExperience\":120,\"delta\":60,\"level\":2,\"sourceEventType\":\"core.raid.resolved\",\"changedAt\":\"2026-01-01T00:00:00Z\"}"
    )
    await get_tree().process_frame
    assert_str(xp_label.text).contains("120")
    assert_str(xp_label.text).contains("2")
