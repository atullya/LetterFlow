namespace LetterTemplatePractice.Services
{
    /// <summary>
    /// Builds the HTML body for the daily news digest email.
    /// Pure static helper — no dependencies needed.
    /// </summary>
    public static class NewsletterEmailBuilder
    {
        public static string Build(IReadOnlyList<NewsStory> stories, string unsubscribeUrl, DateTime date)
        {
            var dateStr = date.ToString("dddd, MMMM d, yyyy");

            var storiesHtml = string.Concat(stories.Select((s, i) => $"""
                <tr>
                  <td style="padding:0 0 28px 0;">
                    <table width="100%" cellpadding="0" cellspacing="0" border="0">
                      <tr>
                        <td style="padding-bottom:6px;">
                          <span style="display:inline-block;background:#f3f4f6;color:#6b7280;
                                       font-size:11px;font-weight:700;letter-spacing:.06em;
                                       text-transform:uppercase;padding:3px 10px;border-radius:999px;">
                            #{i + 1}
                          </span>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding-bottom:8px;">
                          <a href="{s.Url}" target="_blank"
                             style="font-size:18px;font-weight:700;color:#111827;
                                    text-decoration:none;line-height:1.3;">
                            {System.Net.WebUtility.HtmlEncode(s.Title)}
                          </a>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding-bottom:10px;">
                          <p style="margin:0;font-size:14px;color:#374151;line-height:1.6;">
                            {System.Net.WebUtility.HtmlEncode(s.Summary)}
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td>
                          <a href="{s.Url}" target="_blank"
                             style="font-size:13px;color:#2563eb;text-decoration:none;font-weight:600;">
                            Read more &rarr;
                          </a>
                          <span style="color:#d1d5db;margin:0 8px;">|</span>
                          <span style="font-size:12px;color:#9ca3af;">via {System.Net.WebUtility.HtmlEncode(s.Source)}</span>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
                <tr><td style="padding-bottom:28px;border-bottom:1px solid #f3f4f6;"></td></tr>
                """));

            return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                  <title>LetterFlow Daily Digest — {dateStr}</title>
                </head>
                <body style="margin:0;padding:0;background:#f9fafb;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;">
                  <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f9fafb;">
                    <tr>
                      <td align="center" style="padding:40px 16px;">

                        <!-- Card -->
                        <table width="600" cellpadding="0" cellspacing="0" border="0"
                               style="max-width:600px;width:100%;background:#ffffff;
                                      border-radius:12px;overflow:hidden;
                                      box-shadow:0 1px 3px rgba(0,0,0,.08);">

                          <!-- Header -->
                          <tr>
                            <td style="background:#111827;padding:28px 36px;">
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td>
                                    <span style="font-size:22px;font-weight:800;color:#ffffff;
                                                 letter-spacing:-.02em;">
                                      ✉ LetterFlow Stories
                                    </span>
                                  </td>
                                  <td align="right">
                                    <span style="font-size:12px;color:#9ca3af;font-weight:500;">
                                      Daily Digest
                                    </span>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- Date banner -->
                          <tr>
                            <td style="background:#f3f4f6;padding:12px 36px;
                                       border-bottom:1px solid #e5e7eb;">
                              <p style="margin:0;font-size:13px;color:#6b7280;font-weight:600;">
                                {dateStr} &mdash; Top {stories.Count} Stories
                              </p>
                            </td>
                          </tr>

                          <!-- Stories -->
                          <tr>
                            <td style="padding:32px 36px 8px;">
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                {storiesHtml}
                              </table>
                            </td>
                          </tr>

                          <!-- Footer -->
                          <tr>
                            <td style="padding:24px 36px 32px;border-top:1px solid #f3f4f6;">
                              <p style="margin:0 0 8px;font-size:12px;color:#9ca3af;text-align:center;">
                                You're receiving this because you subscribed to LetterFlow Stories daily digest.
                              </p>
                              <p style="margin:0;font-size:12px;text-align:center;">
                                <a href="{unsubscribeUrl}" style="color:#6b7280;text-decoration:underline;">
                                  Unsubscribe
                                </a>
                              </p>
                            </td>
                          </tr>

                        </table>
                        <!-- /Card -->

                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """;
        }
    }
}
