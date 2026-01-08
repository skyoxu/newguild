extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func test_configfile_utf8_roundtrip() -> void:
    # In some CI/userdir override setups, ConfigFile.save("user://...") can fail early.
    # Use an absolute path derived from user:// to keep the test stable across environments.
    await get_tree().process_frame

    var user_path = "user://settings_%d.cfg" % int(Time.get_unix_time_from_system())
    var abs_path = ProjectSettings.globalize_path(user_path)
    DirAccess.make_dir_recursive_absolute(abs_path.get_base_dir())

    var cfg := ConfigFile.new()
    var note := "你好，世界！äöü✓"
    cfg.set_value("app", "volume", 0.66)
    cfg.set_value("app", "lang", "zh")
    cfg.set_value("app", "note", note)
    var err = cfg.save(abs_path)
    assert_int(err).is_equal(0)
    await get_tree().process_frame

    var cfg2 := ConfigFile.new()
    var err2 = cfg2.load(abs_path)
    assert_int(err2).is_equal(0)
    assert_float(float(cfg2.get_value("app", "volume", 0.0))).is_equal(0.66)
    assert_str(str(cfg2.get_value("app", "lang", ""))).is_equal("zh")
    assert_str(str(cfg2.get_value("app", "note", ""))).is_equal(note)
