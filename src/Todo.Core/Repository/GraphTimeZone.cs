using System;
using System.IO;

namespace Todo.Core.Repository;

/// <summary>
/// Microsoft Graph To Do only accepts IANA names (e.g. "Asia/Shanghai") or official
/// Windows time zone names. On macOS, TimeZoneInfo.Local.Id may return a CLDR alias
/// (e.g. "PRC") which the API rejects with "Invalid request".
/// </summary>
public static class GraphTimeZone
{
    public static string GetLocalIanaId()
    {
        var tz = Environment.GetEnvironmentVariable("TZ");
        if (!string.IsNullOrWhiteSpace(tz) && tz.Contains('/'))
            return tz;

        var local = TimeZoneInfo.Local.Id;
        if (local.Contains('/'))
            return local;

        try
        {
            // /etc/localtime -> .../zoneinfo/<Area>/<City>
            var target = new FileInfo("/etc/localtime").ResolveLinkTarget(returnFinalTarget: true);
            var path = target?.FullName;
            const string marker = "/zoneinfo/";
            var idx = !string.IsNullOrEmpty(path) ? path.IndexOf(marker, StringComparison.Ordinal) : -1;
            if (idx >= 0)
                return path![(idx + marker.Length)..].TrimStart('/');
        }
        catch
        {
            // fall through to the default below
        }

        return "Asia/Shanghai";
    }
}
