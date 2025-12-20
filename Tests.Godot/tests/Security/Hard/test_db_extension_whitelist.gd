extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _new_db(name: String) -> Node:
    if not ClassDB.class_exists("SqliteDataStore"):
        push_warning("SqliteDataStore C# class not available; skipping DB extension hard tests.")
        return null

    var db: Node = ClassDB.instantiate("SqliteDataStore")
    db.name = name
    get_tree().get_root().add_child(auto_free(db))
    await get_tree().process_frame
    if not db.has_method("TryOpen"):
        await get_tree().process_frame
    return db


func _today_dir() -> String:
    var d = Time.get_datetime_dict_from_system()
    return "%04d-%02d-%02d" % [d.year, d.month, d.day]


func _audit_path() -> String:
    return "res://logs/ci/%s/security-audit.jsonl" % _today_dir()


func _remove_audit_file() -> void:
    var p: String = _audit_path()
    if FileAccess.file_exists(p):
        var abs: String = ProjectSettings.globalize_path(p)
        DirAccess.remove_absolute(abs)


func _audit_contains_action(audit_path: String, expected_action: String) -> bool:
    if not FileAccess.file_exists(audit_path):
        return false

    var txt: String = FileAccess.get_file_as_string(audit_path)
    if txt == "":
        return false

    var expected = expected_action.to_lower()
    for raw in txt.split("\n", false):
        var line = raw.strip_edges()
        if line == "":
            continue
        var parsed = JSON.parse_string(line)
        if parsed == null:
            continue
        var action = str(parsed.get("action", "")).to_lower()
        if action == expected:
            return true
    return false


func test_try_open_invalid_extension_is_rejected_and_audited() -> void:
    var original_ci: String = OS.get_environment("CI")
    OS.set_environment("CI", "1")
    _remove_audit_file()

    var db = await _new_db("DbExtWhitelist")
    if db == null:
        if original_ci != "":
            OS.set_environment("CI", original_ci)
        else:
            OS.set_environment("CI", "")
        return

    var ok_bad: bool = db.TryOpen("user://security_ext_bad.txt")
    assert_bool(ok_bad).is_false()

    await get_tree().process_frame

    var p: String = _audit_path()
    assert_bool(_audit_contains_action(p, "db.sqlite.invalid_extension")).is_true()

    if original_ci != "":
        OS.set_environment("CI", original_ci)
    else:
        OS.set_environment("CI", "")
