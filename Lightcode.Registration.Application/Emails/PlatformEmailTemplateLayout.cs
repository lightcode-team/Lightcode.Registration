namespace Lightcode.Registration.Application.Emails;

public static class PlatformEmailTemplateLayout
{
    private const string BrandName = "Lightcode";
    private const string BackgroundColor = "#f7f8fb";
    private const string CardColor = "#ffffff";
    private const string BorderColor = "#e1e7ef";
    private const string TextColor = "#172033";
    private const string MutedColor = "#64748b";
    private const string PrimaryColor = "#1264a3";
    private const string AccentColor = "#1d7e6a";

    public static string Build(
        string preheader,
        string eyebrow,
        string title,
        string bodyHtml,
        string? actionUrl = null,
        string? actionLabel = null,
        string? secondaryNoteHtml = null)
    {
        var actionHtml = string.IsNullOrWhiteSpace(actionUrl) || string.IsNullOrWhiteSpace(actionLabel)
            ? string.Empty
            : $$"""
                              <tr>
                                <td style="padding: 8px 32px 24px 32px;">
                                  <a href="{{actionUrl}}" style="display: inline-block; min-width: 176px; padding: 13px 18px; border-radius: 8px; background: {{PrimaryColor}}; color: #ffffff; font-family: Arial, sans-serif; font-size: 15px; font-weight: 700; line-height: 20px; text-align: center; text-decoration: none;">
                                    {{actionLabel}}
                                  </a>
                                </td>
                              </tr>
              """;

        var secondaryNote = string.IsNullOrWhiteSpace(secondaryNoteHtml)
            ? string.Empty
            : $$"""
                              <tr>
                                <td style="padding: 0 32px 24px 32px;">
                                  <div style="padding: 14px 16px; border: 1px solid {{BorderColor}}; border-radius: 8px; background: #f8fafc; color: {{MutedColor}}; font-family: Arial, sans-serif; font-size: 13px; line-height: 20px;">
                                    {{secondaryNoteHtml}}
                                  </div>
                                </td>
                              </tr>
              """;

        return $$"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{title}}</title>
            </head>
            <body style="margin: 0; padding: 0; background: {{BackgroundColor}}; color: {{TextColor}};">
              <div style="display: none; max-height: 0; overflow: hidden; opacity: 0; color: transparent;">
                {{preheader}}
              </div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background: {{BackgroundColor}};">
                <tr>
                  <td align="center" style="padding: 32px 16px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width: 100%; max-width: 600px;">
                      <tr>
                        <td style="padding: 0 0 16px 0;">
                          <div style="font-family: Arial, sans-serif; font-size: 18px; font-weight: 800; letter-spacing: 0; color: {{TextColor}};">
                            <span style="display: inline-block; width: 10px; height: 10px; margin-right: 8px; border-radius: 999px; background: {{AccentColor}};"></span>{{BrandName}}
                          </div>
                        </td>
                      </tr>
                      <tr>
                        <td style="background: {{CardColor}}; border: 1px solid {{BorderColor}}; border-radius: 8px; box-shadow: 0 18px 45px rgba(23, 32, 51, 0.10); overflow: hidden;">
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                            <tr>
                              <td style="height: 4px; background: {{PrimaryColor}}; font-size: 0; line-height: 0;">&nbsp;</td>
                            </tr>
                            <tr>
                              <td style="padding: 32px 32px 8px 32px;">
                                <div style="margin: 0 0 10px 0; color: {{PrimaryColor}}; font-family: Arial, sans-serif; font-size: 12px; font-weight: 800; line-height: 16px; text-transform: uppercase;">
                                  {{eyebrow}}
                                </div>
                                <h1 style="margin: 0; color: {{TextColor}}; font-family: Arial, sans-serif; font-size: 26px; font-weight: 800; line-height: 32px;">
                                  {{title}}
                                </h1>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding: 8px 32px 0 32px; color: {{TextColor}}; font-family: Arial, sans-serif; font-size: 15px; line-height: 24px;">
                                {{bodyHtml}}
                              </td>
                            </tr>
            {{actionHtml}}
            {{secondaryNote}}
                            <tr>
                              <td style="padding: 0 32px 32px 32px;">
                                <p style="margin: 0; color: {{MutedColor}}; font-family: Arial, sans-serif; font-size: 12px; line-height: 18px;">
                                  Esta é uma mensagem automática da plataforma {{BrandName}}.
                                </p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
