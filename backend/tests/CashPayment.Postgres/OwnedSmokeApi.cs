using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dental.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using Dental.Infrastructure.Data;
using Npgsql;

// Chỉ thuộc test harness: chạy API thật với DB, process và thư mục do lượt test sở hữu.
internal sealed class OwnedSmokeApi : IAsyncDisposable
{
    private readonly Process process;
    private readonly TaskCompletionSource<Uri> listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool started, disposed;
    private readonly List<string> diagnostic = [];
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "dental-five-role-" + Guid.NewGuid().ToString("N"));
    public string AdminPhone { get; } = "0999999001";
    public string Password { get; } = "SmokeA1_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public string JwtSecret { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public bool Exited { get; private set; }

    public static void ValidateDatabase(string connectionString, string ownedName)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (!Regex.IsMatch(ownedName, @"\Adental_cash_test_[a-f0-9]{32}\z") || connection.Database != ownedName ||
            connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Refuse API launch outside the local database owned by this run.");
    }

    public OwnedSmokeApi(DbContextOptions<DentalDbContext> options, string ownedName)
    {
        using var db = new DentalDbContext(options);
        var connectionString = db.Database.GetConnectionString()!;
        ValidateDatabase(connectionString, ownedName);
        Directory.CreateDirectory(Root);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(AuthController).Assembly.Location);
        start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(Root);
        start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://127.0.0.1:0");
        // Không kế thừa connection string, SMS, user secrets hoặc hosting startup của máy làm việc.
        start.Environment.Clear();
        foreach (var name in new[] { "PATH", "SystemRoot", "WINDIR", "COMSPEC", "DOTNET_ROOT", "ProgramFiles", "ProgramFiles(x86)" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        foreach (var name in new[] { "TEMP", "TMP", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA" }) start.Environment[name] = Root;
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ConnectionStrings__Default"] = connectionString;
        start.Environment["IdentitySecurity__HmacKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        start.Environment["Jwt__Secret"] = JwtSecret;
        start.Environment["Jwt__Issuer"] = "DentalClinic";
        start.Environment["Jwt__Audience"] = "DentalClinicClient";
        start.Environment["Seed__AdminPhone"] = AdminPhone;
        start.Environment["Seed__AdminPassword"] = Password;
        start.Environment["Seed__DemoUsers"] = "false";
        start.Environment["Seed__Phase1"] = "false";
        foreach (var prefix in new[] { "PatientActivation", "PasswordRecovery", "BookingOtp", "StaffPhoneChange" })
            start.Environment[prefix + "__Sms__Enabled"] = "false";
        start.Environment["Logging__LogLevel__Default"] = "None";
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            // Chỉ lấy địa chỉ Kestrel của process này; không lưu/in response, OTP hoặc log API.
            var match = Regex.Match(args.Data ?? "", @"Now listening on: (http://127\.0\.0\.1:[0-9]+)");
            if (match.Success) listening.TrySetResult(new Uri(match.Groups[1].Value));
        };
        process.ErrorDataReceived += (_, args) =>
        {
            var match = Regex.Match(args.Data ?? "", @"(?:Unhandled exception\. |---> )([A-Za-z0-9_.]+)|^\s+at ([A-Za-z0-9_.<>+`]+)");
            if (match.Success) lock (diagnostic) if (diagnostic.Count < 6) diagnostic.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        };
        process.Exited += (_, _) => listening.TrySetException(new InvalidOperationException("Owned API exited before becoming ready; raw logs suppressed."));
    }

    public async Task<HttpClient> StartAsync(bool failStartup = false)
    {
        if (failStartup) process.StartInfo.Environment["IdentitySecurity__HmacKey"] = "";
        started = process.Start();
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        Uri address;
        try { address = await listening.Task.WaitAsync(TimeSpan.FromSeconds(90)); }
        catch (InvalidOperationException)
        {
            await process.WaitForExitAsync();
            lock (diagnostic) throw new InvalidOperationException("FAIL: Owned API startup failed [" + string.Join(", ", diagnostic) + "]. Raw details suppressed.");
        }
        if (address.Host != "127.0.0.1" || address.Port <= 0 || process.HasExited)
            throw new InvalidOperationException("Invalid owned API address.");
        var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30) };
        try
        {
            using var health = await client.GetAsync("/api/appointment-requests/catalog");
            if (!health.IsSuccessStatusCode) throw new InvalidOperationException("Owned API catalog health check failed.");
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        if (started && !process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        var path = Path.GetFullPath(Root);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(Path.GetFileName(path), @"\Adental-five-role-[a-f0-9]{32}\z"))
            throw new InvalidOperationException("Refuse cleanup outside owned temporary directory.");
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        Exited = !started || process.HasExited; process.Dispose(); disposed = true;
    }
}
