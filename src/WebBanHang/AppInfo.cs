using System.Globalization;
using System.Reflection;

namespace WebBanHang;

public static class AppInfo
{
    // Jenkins builds with -p:Version=1.0.<BUILD_NUMBER>, so the footer shows which build is live.
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "dev";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static string ToVnd(this decimal amount) => amount.ToString("N0", Vietnamese) + " ₫";
}
