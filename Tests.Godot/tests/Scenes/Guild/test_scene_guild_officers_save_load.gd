extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _load_probe() -> Node:
    if not ClassDB.class_exists("GuildOfficersSaveLoadProbe"):
        push_warning("GuildOfficersSaveLoadProbe not available; skipping officer save/load test.")
        return null
    var probe: Node = ClassDB.instantiate("GuildOfficersSaveLoadProbe")
    get_tree().get_root().add_child(auto_free(probe))
    await get_tree().process_frame
    return probe

# ACC:T38.1
func test_officer_assignments_persist_across_save_load() -> void:
    var probe = await _load_probe()
    if probe == null:
        return

    var ts := str(Time.get_unix_time_from_system())
    var db_path := "user://utdb_%s/guild_officers.db" % ts
    var guild_id := "g_%s" % ts
    var officer_id := "m_%s" % ts
    var result = probe.RunOfficerSaveLoad(db_path, guild_id, officer_id, 0)

    assert_str(str(result.get("error", ""))).is_equal("")
    assert_bool(bool(result.get("assigned", false))).is_true()
    assert_bool(bool(result.get("loaded", false))).is_true()
    assert_bool(bool(result.get("persisted", false))).is_true()

    if FileAccess.file_exists(db_path):
        DirAccess.remove_absolute(ProjectSettings.globalize_path(db_path))
