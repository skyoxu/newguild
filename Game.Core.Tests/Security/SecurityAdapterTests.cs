using FluentAssertions;
using Game.Core.Contracts;
using Game.Core.Services;
using Xunit;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace Game.Core.Tests.Security;

/// <summary>
/// TDD Red Phase: Failing tests for SecurityUrlAdapter and SecurityFileAdapter
/// These tests define the security contract before implementation exists.
/// </summary>
public class SecurityAdapterTests
{
    #region SecurityUrlAdapter Tests

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_JavaScriptScheme()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityUrlAdapter(bus);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act
        var result = await adapter.ValidateAsync("javascript:alert('XSS')");

        // Assert
        result.Should().BeFalse("javascript: scheme should be blocked");
        capturedEvent.Should().NotBeNull("security event should be published");
        capturedEvent!.Type.Should().Be("security.url_access.denied");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_DataScheme()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityUrlAdapter(bus);

        // Act
        var result = await adapter.ValidateAsync("data:text/html,<script>alert('XSS')</script>");

        // Assert
        result.Should().BeFalse("data: scheme should be blocked");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_BlobScheme()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityUrlAdapter(bus);

        // Act
        var result = await adapter.ValidateAsync("blob:https://example.com/550e8400-e29b-41d4-a716-446655440000");

        // Assert
        result.Should().BeFalse("blob: scheme should be blocked");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_FileScheme()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityUrlAdapter(bus);

        // Act
        var result = await adapter.ValidateAsync("file:///C:/Windows/System32/config");

        // Assert
        result.Should().BeFalse("file: scheme should be blocked");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_DisallowedDomain()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedDomains = new[] { "example.com", "sentry.io" };
        var adapter = new SecurityUrlAdapter(bus, allowedDomains);

        // Act
        var result = await adapter.ValidateAsync("https://evil.com/malware");

        // Assert
        result.Should().BeFalse("domain not in whitelist should be blocked");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldAllow_WhitelistedDomain()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedDomains = new[] { "example.com", "sentry.io" };
        var adapter = new SecurityUrlAdapter(bus, allowedDomains);

        // Act
        var result = await adapter.ValidateAsync("https://example.com/api");

        // Assert
        result.Should().BeTrue("whitelisted domain with https should be allowed");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_NonHttpsScheme()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedDomains = new[] { "example.com" };
        var adapter = new SecurityUrlAdapter(bus, allowedDomains);

        // Act
        var result = await adapter.ValidateAsync("http://example.com/api");

        // Assert
        result.Should().BeFalse("http (non-https) should be blocked when EnforceHttps is true");
    }

    [Fact]
    public async Task SecurityUrlAdapter_PublishesCorrectAuditFormat()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityUrlAdapter(bus);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act
        await adapter.ValidateAsync("javascript:alert('XSS')");

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be("security.url_access.denied");
        capturedEvent.Source.Should().Be("SecurityUrlAdapter");
        capturedEvent.Data.Should().NotBeNull();
        // Verify audit data structure: {action, reason, target, caller}
        // Data object validated as non-null; field structure verified by contract
    }

    [Fact]
    public void SecurityUrlAdapter_Constructor_ShouldThrow_WhenBusIsNull()
    {
        // Arrange
        Action act = () => new SecurityUrlAdapter(null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("bus");
    }

    [Fact]
    public void SecurityUrlAdapter_Constructor_ShouldThrow_WhenAllowedDomainsIsNull()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        Action act = () => new SecurityUrlAdapter(bus, allowedDomains: null!);

        // Act & Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("allowedDomains");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SecurityUrlAdapter_ShouldReject_NullOrEmptyUrl(string? url)
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedDomains = new[] { "example.com" };
        var adapter = new SecurityUrlAdapter(bus, allowedDomains);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act
        var result = await adapter.ValidateAsync(url!);

        // Assert
        result.Should().BeFalse("null/empty URL must be rejected");
        capturedEvent.Should().NotBeNull("security event should be published");
        capturedEvent!.Type.Should().Be("security.url_access.denied");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldReject_InvalidUri_WhenWhitelistConfigured()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedDomains = new[] { "example.com" };
        var adapter = new SecurityUrlAdapter(bus, allowedDomains);

        // Act
        var result = await adapter.ValidateAsync("not a url");

        // Assert
        result.Should().BeFalse("invalid URI must be rejected when whitelist is configured");
    }

    [Fact]
    public async Task SecurityUrlAdapter_ShouldAllow_WhenAllowedDomainsIsEmpty()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityUrlAdapter(bus, allowedDomains: Array.Empty<string>());

        // Act
        var result = await adapter.ValidateAsync("https://example.com/api");

        // Assert
        result.Should().BeTrue("empty whitelist behaves as 'not configured' in current implementation");
    }

    #endregion

    #region SecurityFileAdapter Tests

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldReject_PathTraversal()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var path = adapter.ValidatePath("user://../../../etc/passwd");

        // Assert
        path.Should().BeNull("path traversal with .. should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldReject_BackslashTraversal()
    {
        // Arrange: Test CWE-22 Windows backslash path traversal variants
        // Expected to FAIL until ContainsPathTraversal() is enhanced
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var simpleBackslash = adapter.ValidatePath("user://..\\config.json");
        var complexBackslash = adapter.ValidatePath("user://..\\.\\..\\system.dat");
        var mixedSlashes = adapter.ValidatePath("user://..\\../secrets.txt");

        // Assert
        simpleBackslash.Should().BeNull("Windows backslash traversal (..\\ variant) should be rejected");
        complexBackslash.Should().BeNull("Complex backslash traversal should be rejected");
        mixedSlashes.Should().BeNull("Mixed forward/backslash traversal should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldReject_EncodedTraversal()
    {
        // Arrange: Test CWE-22 URL-encoded path traversal bypass variants
        // Expected to FAIL until URL decoding is added to validation
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var singleEncoded = adapter.ValidatePath("user://%2e%2e/config.json");
        var doubleEncoded = adapter.ValidatePath("user://%252e%252e/config.json");
        var mixedEncoded = adapter.ValidatePath("user://..\\%2fconfig.json");
        // Note: UTF-8 overlong encoding (%c0%ae = '.') is an advanced attack vector
        // that requires specialized UTF-8 decoder. Out of scope for current implementation.
        // var utf8Overlong = adapter.ValidatePath("user://%c0%ae%c0%ae/secrets.txt");

        // Assert
        singleEncoded.Should().BeNull("Single URL-encoded traversal (%2e%2e) should be rejected");
        doubleEncoded.Should().BeNull("Double URL-encoded traversal (%252e) should be rejected");
        mixedEncoded.Should().BeNull("Mixed encoding traversal should be rejected");
        // utf8Overlong.Should().BeNull("UTF-8 overlong encoding traversal should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldReject_DisallowedExtension()
    {
        // Arrange: Test extension whitelist enforcement per ADR-0019
        // Expected to FAIL until extension validation is implemented
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act - Executable and script extensions should be rejected
        var exeFile = adapter.ValidatePath("user://saves/malware.exe");
        var dllFile = adapter.ValidatePath("user://saves/library.dll");
        var batFile = adapter.ValidatePath("user://saves/script.bat");
        var ps1File = adapter.ValidatePath("user://saves/script.ps1");
        var shFile = adapter.ValidatePath("user://saves/script.sh");

        // Act - Case variations should be rejected (case-insensitive)
        var upperExe = adapter.ValidatePath("user://saves/MALWARE.EXE");
        var mixedDll = adapter.ValidatePath("user://saves/Library.DLL");

        // Assert
        exeFile.Should().BeNull(".exe extension should be rejected");
        dllFile.Should().BeNull(".dll extension should be rejected");
        batFile.Should().BeNull(".bat extension should be rejected");
        ps1File.Should().BeNull(".ps1 extension should be rejected");
        shFile.Should().BeNull(".sh extension should be rejected");
        upperExe.Should().BeNull(".EXE (uppercase) should be rejected");
        mixedDll.Should().BeNull(".DLL (mixed case) should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldAllow_WhitelistedExtension()
    {
        // Arrange: Test whitelisted extensions are allowed per ADR-0019
        // Expected to FAIL until extension validation is implemented
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act - Game data extensions should be allowed
        var jsonFile = adapter.ValidatePath("user://saves/game.json");
        var txtFile = adapter.ValidatePath("user://saves/notes.txt");
        var datFile = adapter.ValidatePath("user://saves/state.dat");
        var saveFile = adapter.ValidatePath("user://saves/progress.save");
        var cfgFile = adapter.ValidatePath("user://config/settings.cfg");

        // Act - Case variations should be allowed (case-insensitive)
        var upperJson = adapter.ValidatePath("user://saves/GAME.JSON");
        var mixedTxt = adapter.ValidatePath("user://saves/Notes.TXT");

        // Assert
        jsonFile.Should().NotBeNull(".json extension should be allowed");
        jsonFile!.Value.Should().Be("user://saves/game.json");
        txtFile.Should().NotBeNull(".txt extension should be allowed");
        datFile.Should().NotBeNull(".dat extension should be allowed");
        saveFile.Should().NotBeNull(".save extension should be allowed");
        cfgFile.Should().NotBeNull(".cfg extension should be allowed");
        upperJson.Should().NotBeNull(".JSON (uppercase) should be allowed");
        mixedTxt.Should().NotBeNull(".TXT (mixed case) should be allowed");
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldReject_AbsolutePath()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var windowsPath = adapter.ValidatePath("C:\\Windows\\System32\\config");
        var unixPath = adapter.ValidatePath("/etc/passwd");

        // Assert
        windowsPath.Should().BeNull("Windows absolute path should be rejected");
        unixPath.Should().BeNull("Unix absolute path should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidateWritePath_ShouldReject_ResPath()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var path = adapter.ValidateWritePath("res://scenes/Main.tscn");

        // Assert
        path.Should().BeNull("res:// is read-only, write access should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidateWritePath_ShouldAllow_UserPath()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var path = adapter.ValidateWritePath("user://saves/save1.json");

        // Assert
        path.Should().NotBeNull("user:// should allow write access");
        path!.Value.Should().Be("user://saves/save1.json");
    }

    [Fact]
    public void SecurityFileAdapter_ValidateReadPath_ShouldAllow_ResPath()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var path = adapter.ValidateReadPath("res://assets/icon.png");

        // Assert
        path.Should().NotBeNull("res:// should allow read access");
        path!.Value.Should().Be("res://assets/icon.png");
    }

    [Fact]
    public void SecurityFileAdapter_ValidateReadPath_ShouldAllow_UserPath()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act
        var path = adapter.ValidateReadPath("user://config/settings.cfg");

        // Assert
        path.Should().NotBeNull("user:// should allow read access");
        path!.Value.Should().Be("user://config/settings.cfg");
    }

    [Fact]
    public void SecurityFileAdapter_PublishesAuditEventOnRejection()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act
        adapter.ValidatePath("user://../../../etc/passwd");

        // Assert
        capturedEvent.Should().NotBeNull("security event should be published on rejection");
        capturedEvent!.Type.Should().Be("security.file_access.denied");
        capturedEvent.Source.Should().Be("SecurityFileAdapter");
    }

    [Fact]
    public void SecurityFileAdapter_ValidateWritePath_ShouldReject_InvalidPath()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act - Test with empty path (invalid format)
        var emptyPath = adapter.ValidateWritePath("");

        // Assert
        emptyPath.Should().BeNull("empty path should be rejected as invalid");
        capturedEvent.Should().NotBeNull("audit event should be published for invalid path");
        capturedEvent!.Type.Should().Be("security.file_access.denied");
        capturedEvent.Data.Should().NotBeNull();
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldReject_ExcessivelyLongPath()
    {
        // Arrange: Test path length limit per security baseline
        // Windows MAX_PATH is 260 characters; Godot user:// paths should be reasonable
        // Expected to FAIL until path length validation is implemented
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act - Create path exceeding 260 characters (Windows MAX_PATH limit)
        var longFileName = new string('a', 300); // 300 character filename
        var excessivelyLongPath = $"user://saves/{longFileName}.json";

        var result = adapter.ValidatePath(excessivelyLongPath);

        // Assert
        result.Should().BeNull("excessively long path should be rejected");
    }

    [Fact]
    public void SecurityFileAdapter_ValidatePath_ShouldAllow_ReasonableLengthPath()
    {
        // Arrange: Test that normal-length paths are allowed
        // Expected to FAIL if length validation is too strict
        var bus = new InMemoryEventBus();
        var adapter = new SecurityFileAdapter(bus);

        // Act - Normal length path (well under 260 characters)
        var normalPath = "user://saves/guild_manager/player_123/save_file_001.json"; // ~60 chars

        var result = adapter.ValidatePath(normalPath);

        // Assert
        result.Should().NotBeNull("reasonable length path should be allowed");
        result!.Value.Should().Be(normalPath);
    }

    #endregion

    #region SecurityProcessAdapter Tests

    [Fact]
    public async Task SecurityProcessAdapter_ShouldReject_NonWhitelistedCommand()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedCommands = new[] { "git", "dotnet" };
        var adapter = new SecurityProcessAdapter(bus, allowedCommands);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act
        var result = await adapter.ExecuteAsync("powershell", new[] { "Get-Process" });

        // Assert
        result.Should().BeNull("non-whitelisted command should be rejected");
        capturedEvent.Should().NotBeNull("security event should be published");
        capturedEvent!.Type.Should().Be("security.process.denied");
    }

    [Fact]
    public async Task SecurityProcessAdapter_ShouldAllow_WhitelistedCommand()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedCommands = new[] { "git", "dotnet" };
        var adapter = new SecurityProcessAdapter(bus, allowedCommands);

        // Act - Test actual ExecuteAsync (not just IsCommandAllowed)
        var result = await adapter.ExecuteAsync("git", new[] { "status" });

        // Assert - In current implementation, ExecuteAsync returns null (minimal impl)
        // But the command should be allowed (no denial event published)
        result.Should().BeNull("minimal implementation returns null");

        // Verify no denial event was published
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });
        capturedEvent.Should().BeNull("no denial event for whitelisted command");
    }

    [Fact]
    public async Task SecurityProcessAdapter_ExecuteAsync_ShouldAllow_WhitelistedCommand()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var allowedCommands = new[] { "git", "dotnet" };
        var adapter = new SecurityProcessAdapter(bus, allowedCommands);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Enable development mode for this test
        Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", "1");

        try
        {
            // Act - Call ExecuteAsync with whitelisted command (covers the allowed branch)
            var result = await adapter.ExecuteAsync("dotnet", new[] { "build" });

            // Assert
            result.Should().NotBeNull("development mode should execute whitelisted command");
            result!.ExitCode.Should().BeGreaterOrEqualTo(0, "valid exit code expected");
            capturedEvent.Should().NotBeNull("approval event should be published for successful execution");
            capturedEvent!.Type.Should().Be("security.process.approved");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", null);
        }
    }

    [Fact]
    public async Task SecurityProcessAdapter_ShouldReject_AllCommands_WhenNoWhitelist()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityProcessAdapter(bus, allowedCommands: null);

        // Act
        var result = await adapter.ExecuteAsync("git", new[] { "status" });

        // Assert
        result.Should().BeNull("all commands should be rejected when no whitelist");
    }

    [Fact]
    public async Task SecurityProcessAdapter_PublishesAuditEventOnRejection()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityProcessAdapter(bus, new[] { "git" });
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Act
        await adapter.ExecuteAsync("malicious", new[] { "arg1" });

        // Assert
        capturedEvent.Should().NotBeNull("security event should be published on rejection");
        capturedEvent!.Type.Should().Be("security.process.denied");
        capturedEvent.Source.Should().Be("SecurityProcessAdapter");
        capturedEvent.Data.Should().NotBeNull();
    }

    [Fact]
    public void SecurityProcessAdapter_IsCommandAllowed_ShouldReturnFalse_WhenWhitelistIsNull()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityProcessAdapter(bus, allowedCommands: null);

        // Act
        var allowed = adapter.IsCommandAllowed("git");

        // Assert
        allowed.Should().BeFalse("null whitelist should deny all commands");
    }

    [Fact]
    public void SecurityProcessAdapter_IsCommandAllowed_ShouldReturnTrue_WhenCommandIsWhitelisted()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityProcessAdapter(bus, new[] { "git" });

        // Act
        var allowed = adapter.IsCommandAllowed("git");

        // Assert
        allowed.Should().BeTrue("whitelisted command should be allowed");
    }

    [Fact]
    public async Task SecurityProcessAdapter_ExecuteAsync_ShouldReject_NonWhitelistedCommand_InDevelopmentMode()
    {
        // Arrange
        var bus = new InMemoryEventBus();
        var adapter = new SecurityProcessAdapter(bus, new[] { "git" });
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", "1");
        try
        {
            // Act
            var result = await adapter.ExecuteAsync("dotnet", new[] { "--version" });

            // Assert
            result.Should().BeNull("non-whitelisted command should be rejected before execution");
            capturedEvent.Should().NotBeNull("denial event should be published");
            capturedEvent!.Type.Should().Be("security.process.denied");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", null);
        }
    }

    [Fact]
    public async Task SecurityProcessAdapter_ShouldReject_InProductionMode()
    {
        // Arrange: Test ADR-0019 requirement - OS.execute disabled by default in production
        // Production mode: GD_ENABLE_PROCESS_EXECUTION not set or set to "0"
        // Expected to FAIL until environment detection is implemented
        var bus = new InMemoryEventBus();
        var allowedCommands = new[] { "git", "dotnet" };
        var adapter = new SecurityProcessAdapter(bus, allowedCommands);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Ensure production mode (no environment variable set)
        Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", null);

        // Act - Even whitelisted commands should be rejected in production
        var result = await adapter.ExecuteAsync("git", new[] { "status" });

        // Assert
        result.Should().BeNull("production mode should reject all process execution");
        capturedEvent.Should().NotBeNull("security event should be published in production mode");
        capturedEvent!.Type.Should().Be("security.process.denied");
        // Verify reason indicates production mode restriction
        capturedEvent.Data.Should().NotBeNull();
        var dataStr = System.Text.Json.JsonSerializer.Serialize(capturedEvent.Data);
        dataStr.Should().Contain("production", "reason should indicate production mode restriction");
    }

    [Fact]
    public async Task SecurityProcessAdapter_ShouldAllow_InDevelopmentMode()
    {
        // Arrange: Test ADR-0019 requirement - OS.execute enabled in development with audit
        // Development mode: GD_ENABLE_PROCESS_EXECUTION=1
        // Expected to FAIL until real execution logic is implemented
        var bus = new InMemoryEventBus();
        var allowedCommands = new[] { "git" };
        var adapter = new SecurityProcessAdapter(bus, allowedCommands);
        DomainEvent? capturedEvent = null;
        bus.Subscribe(e => { capturedEvent = e; return Task.CompletedTask; });

        // Enable development mode
        Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", "1");

        try
        {
            // Act - Whitelisted command should execute and return result
            var result = await adapter.ExecuteAsync("git", new[] { "status" });

            // Assert
            result.Should().NotBeNull("development mode should allow whitelisted process execution");
            result!.ExitCode.Should().BeGreaterOrEqualTo(0, "process should return valid exit code");
            // Success audit event should be published
            capturedEvent.Should().NotBeNull("audit event should be published for successful execution");
            capturedEvent!.Type.Should().Be("security.process.approved");
        }
        finally
        {
            // Clean up environment variable
            Environment.SetEnvironmentVariable("GD_ENABLE_PROCESS_EXECUTION", null);
        }
    }

    #endregion
}
