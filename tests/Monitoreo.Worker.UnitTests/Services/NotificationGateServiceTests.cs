// BEGIN-TEST::BE-667::2026-03-17::AHL::Tests unitarios para NotificationGateService: kill switch, flags, cooldown, supresiones
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Notification;

namespace Monitoreo.Worker.UnitTests.Services;

public class NotificationGateServiceTests
{
    private readonly Mock<IAmazonSimpleSystemsManagement> _ssmMock = new();
    private readonly Mock<Monitoreo.Worker.Services.Configuration.IConfigurationProvider> _configMock = new();
    private readonly Mock<ILogger<NotificationGateService>> _loggerMock = new();
    private readonly IConfiguration _configuration;

    public NotificationGateServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:Environment"] = "Development"
            })
            .Build();

        _ssmMock.Setup(s => s.GetParameterAsync(It.IsAny<GetParameterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetParameterResponse
            {
                Parameter = new Parameter { Value = "true" }
            });
    }

    private NotificationGateService CreateService() =>
        new(_ssmMock.Object, _configMock.Object, _configuration, _loggerMock.Object);

    private CountryConfig CreateConfig(bool emailEnabled = true, bool whatsAppEnabled = true, int cooldown = 15) =>
        new()
        {
            CountryCode = "GT", Enabled = true,
            AsmxEndpoint = "https://test", NucLoginEndpoint = "https://test",
            NucCertEndpoint = "https://test", AsmxTemplatePath = "t.xml",
            NucTemplatePath = "t.xml", TaxId = "123", Requestor = "R",
            NucUsername = "u", NucAuthMode = "dynamic",
            NotificationsEmailEnabled = emailEnabled,
            NotificationsWhatsAppEnabled = whatsAppEnabled,
            NotificationCooldownMinutes = cooldown
        };

    [Fact]
    public async Task EvaluateAsync_KillSwitchOff_SuppressesNotification()
    {
        _ssmMock.Setup(s => s.GetParameterAsync(It.IsAny<GetParameterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetParameterResponse { Parameter = new Parameter { Value = "false" } });

        var sut = CreateService();
        var result = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.Email, CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.SuppressedReason.Should().Contain("Kill switch");
    }

    [Fact]
    public async Task EvaluateAsync_EmailDisabled_SuppressesEmail()
    {
        _configMock.Setup(c => c.LoadCountryAsync("GT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig(emailEnabled: false));

        var sut = CreateService();
        var result = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.Email, CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.SuppressedReason.Should().Contain("Email deshabilitado");
    }

    [Fact]
    public async Task EvaluateAsync_WhatsAppDisabled_SuppressesWhatsApp()
    {
        _configMock.Setup(c => c.LoadCountryAsync("GT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig(whatsAppEnabled: false));

        var sut = CreateService();
        var result = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.WhatsApp, CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.SuppressedReason.Should().Contain("WhatsApp deshabilitado");
    }

    [Fact]
    public async Task EvaluateAsync_AllEnabled_AllowsNotification()
    {
        _configMock.Setup(c => c.LoadCountryAsync("GT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig());

        var sut = CreateService();
        var result = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.Email, CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
        result.SuppressedReason.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_Cooldown_SuppressesSecondCall()
    {
        _configMock.Setup(c => c.LoadCountryAsync("GT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig(cooldown: 60));

        var sut = CreateService();

        var first = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.Email, CancellationToken.None);
        first.IsAllowed.Should().BeTrue();

        var second = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.Email, CancellationToken.None);
        second.IsAllowed.Should().BeFalse();
        second.SuppressedReason.Should().Contain("Cooldown");
    }

    [Fact]
    public async Task EvaluateAsync_SsmNotFound_DefaultsToEnabled()
    {
        _ssmMock.Setup(s => s.GetParameterAsync(It.IsAny<GetParameterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ParameterNotFoundException("not found"));
        _configMock.Setup(c => c.LoadCountryAsync("GT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig());

        var sut = CreateService();
        var result = await sut.EvaluateAsync("GT", "ASMX", NotificationChannel.Email, CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
    }
}
// END-TEST::BE-667::2026-03-17::AHL::Tests unitarios para NotificationGateService: kill switch, flags, cooldown, supresiones
