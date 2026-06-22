using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails;
using Lightcode.Registration.Domain.Entities;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class EmailDispatchMessageProcessorTests
{
    [Fact]
    public async Task System_email_with_template_uses_platform_repository_and_master_sender()
    {
        var platformRepo = new FakePlatformTemplateRepository();
        platformRepo.ByKey[PlatformEmailTemplates.PlatformAdminTwoFactorCode] = new EmailTemplate
        {
            Id = "template-platform-2fa",
            TenantId = PlatformEmailTemplates.TenantId,
            Key = PlatformEmailTemplates.PlatformAdminTwoFactorCode,
            Subject = "Código {{code}}",
            HtmlBody = "<p>Olá {{username}}</p>",
            TextBody = "Olá {{username}}"
        };

        var systemSender = new FakeSystemOutboundMailSender();
        var tenantSender = new FakeOutboundMailSender();
        var processor = CreateProcessor(platformRepo: platformRepo, systemSender: systemSender, tenantSender: tenantSender);

        var processed = await processor.ProcessAsync(new EmailDispatchQueueMessage(
            TenantId: PlatformEmailTemplates.TenantId,
            TemplateId: null,
            TemplateKey: PlatformEmailTemplates.PlatformAdminTwoFactorCode,
            To: "admin@example.com",
            Parameters: new Dictionary<string, string>
            {
                ["username"] = "Ana",
                ["code"] = "123456"
            },
            SystemEmail: true));

        processed.Should().BeTrue();
        systemSender.Sent.Should().ContainSingle();
        systemSender.Sent[0].Should().Be(("admin@example.com", "Código 123456", "<p>Olá Ana</p>", "Olá Ana"));
        tenantSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task System_email_without_template_uses_inline_fallback()
    {
        var systemSender = new FakeSystemOutboundMailSender();
        var processor = CreateProcessor(systemSender: systemSender);

        var processed = await processor.ProcessAsync(new EmailDispatchQueueMessage(
            TenantId: PlatformEmailTemplates.TenantId,
            TemplateId: null,
            TemplateKey: null,
            To: "admin@example.com",
            Parameters: null,
            SystemEmail: true,
            Subject: "Assunto inline",
            HtmlBody: "<p>Corpo inline</p>",
            TextBody: null));

        processed.Should().BeTrue();
        systemSender.Sent.Should().ContainSingle();
        systemSender.Sent[0].Should().Be(("admin@example.com", "Assunto inline", "<p>Corpo inline</p>", null));
    }

    [Fact]
    public async Task Tenant_email_still_uses_tenant_repository_and_tenant_sender()
    {
        var tenantRepo = new FakeTenantTemplateRepository();
        tenantRepo.ByTenantAndKey[("tenant-1", "account-login-2fa-code")] = new EmailTemplate
        {
            Id = "template-tenant-2fa",
            TenantId = "tenant-1",
            Key = "account-login-2fa-code",
            Subject = "Login {{code}}",
            HtmlBody = "<p>{{username}}</p>",
            TextBody = "{{username}}"
        };

        var systemSender = new FakeSystemOutboundMailSender();
        var tenantSender = new FakeOutboundMailSender();
        var processor = CreateProcessor(tenantRepo: tenantRepo, systemSender: systemSender, tenantSender: tenantSender);

        var processed = await processor.ProcessAsync(new EmailDispatchQueueMessage(
            TenantId: "tenant-1",
            TemplateId: null,
            TemplateKey: "account-login-2fa-code",
            To: "user@example.com",
            Parameters: new Dictionary<string, string>
            {
                ["username"] = "Bruno",
                ["code"] = "654321"
            }));

        processed.Should().BeTrue();
        tenantSender.Sent.Should().ContainSingle();
        tenantSender.Sent[0].Should().Be(("tenant-1", "user@example.com", "Login 654321", "<p>Bruno</p>", "Bruno"));
        systemSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task System_email_with_missing_template_is_not_sent()
    {
        var systemSender = new FakeSystemOutboundMailSender();
        var processor = CreateProcessor(systemSender: systemSender);

        var processed = await processor.ProcessAsync(new EmailDispatchQueueMessage(
            TenantId: PlatformEmailTemplates.TenantId,
            TemplateId: null,
            TemplateKey: "missing-template",
            To: "admin@example.com",
            Parameters: null,
            SystemEmail: true));

        processed.Should().BeFalse();
        systemSender.Sent.Should().BeEmpty();
    }

    private static EmailDispatchMessageProcessor CreateProcessor(
        FakeTenantTemplateRepository? tenantRepo = null,
        FakePlatformTemplateRepository? platformRepo = null,
        FakeOutboundMailSender? tenantSender = null,
        FakeSystemOutboundMailSender? systemSender = null) =>
        new(
            tenantRepo ?? new FakeTenantTemplateRepository(),
            platformRepo ?? new FakePlatformTemplateRepository(),
            tenantSender ?? new FakeOutboundMailSender(),
            systemSender ?? new FakeSystemOutboundMailSender());

    private sealed class FakeTenantTemplateRepository : IEmailTemplateRepository
    {
        public Dictionary<(string TenantId, string Key), EmailTemplate> ByTenantAndKey { get; } = [];

        public Task<IReadOnlyList<EmailTemplate>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EmailTemplate>>([]);

        public Task<EmailTemplate?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
            Task.FromResult<EmailTemplate?>(ByTenantAndKey.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id));

        public Task<EmailTemplate?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByTenantAndKey.GetValueOrDefault((tenantId, key)));

        public Task InsertAsync(EmailTemplate entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAsync(EmailTemplate entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePlatformTemplateRepository : IPlatformEmailTemplateRepository
    {
        public Dictionary<string, EmailTemplate> ByKey { get; } = new(StringComparer.Ordinal);

        public Task<EmailTemplate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult<EmailTemplate?>(ByKey.Values.FirstOrDefault(x => x.Id == id));

        public Task<EmailTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByKey.GetValueOrDefault(key));

        public Task InsertIfMissingAsync(EmailTemplate template, CancellationToken cancellationToken = default)
        {
            ByKey.TryAdd(template.Key, template);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboundMailSender : IOutboundMailSender
    {
        public List<(string TenantId, string To, string Subject, string? HtmlBody, string? TextBody)> Sent { get; } = [];

        public Task SendAsync(
            string tenantId,
            string to,
            string subject,
            string? htmlBody,
            string? textBody,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((tenantId, to, subject, htmlBody, textBody));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSystemOutboundMailSender : ISystemOutboundMailSender
    {
        public List<(string To, string Subject, string? HtmlBody, string? TextBody)> Sent { get; } = [];

        public Task SendAsync(
            string to,
            string subject,
            string? htmlBody,
            string? textBody,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((to, subject, htmlBody, textBody));
            return Task.CompletedTask;
        }
    }
}
