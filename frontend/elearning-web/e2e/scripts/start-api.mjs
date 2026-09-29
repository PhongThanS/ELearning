// Khởi động backend cho E2E: database riêng (xóa và tạo lại mỗi lần), build Release (không đụng tới
// bản Debug mà API dev đang chạy), nới giới hạn tần suất, job hết giờ quét mỗi 5 giây.
//
// Biến môi trường:
//   E2E_SQL       connection string tới SQL Server (không cần Database). Mặc định: localhost, Windows auth.
//   E2E_DATABASE  tên database. Mặc định: ELearningE2E.
//   E2E_API_PORT  cổng HTTP. Mặc định: 5236.
//   E2E_KEEP_DB=1 giữ database cũ (không xóa).
import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(fileURLToPath(new URL(".", import.meta.url)), "../../../..");
const backend = path.join(root, "backend");
const output = path.join(backend, "src", "ELearning.Api", "bin", "Release", "net10.0");
const port = process.env.E2E_API_PORT ?? "5236";
const server = process.env.E2E_SQL ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True";
const connection = `${server.replace(/;?$/, ";")}Database=${process.env.E2E_DATABASE ?? "ELearningE2E"}`;
const efProject = ["-p", "src/ELearning.Infrastructure", "-s", "src/ELearning.Api", "--no-build", "--configuration", "Release"];
// Lệnh ef dựng host của API nên đọc connection string từ biến môi trường
const efEnv = { ...process.env, ASPNETCORE_ENVIRONMENT: "Development", ConnectionStrings__DefaultConnection: connection };

function run(command, args, env = process.env) {
  console.log(`> ${command} ${args.join(" ")}`);
  const result = spawnSync(command, args, { cwd: backend, env, stdio: "inherit", shell: process.platform === "win32" });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

run("dotnet", ["tool", "restore"]);
run("dotnet", ["build", "src/ELearning.Api", "-c", "Release"]);
if (process.env.E2E_KEEP_DB !== "1") {
  run("dotnet", ["ef", "database", "drop", "--force", ...efProject], efEnv);
}
run("dotnet", ["ef", "database", "update", ...efProject], efEnv);

const api = spawn("dotnet", [path.join(output, "ELearning.Api.dll")], {
  cwd: output,
  stdio: "inherit",
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://localhost:${port}`,
    ConnectionStrings__DefaultConnection: connection,
    App__PublicOrigin: process.env.E2E_BASE_URL ?? `http://localhost:${process.env.E2E_WEB_PORT ?? "5273"}`,
    RateLimits__AuthLoginPerMinute: "1000",
    RateLimits__AuthRefreshPerMinute: "1000",
    RateLimits__AttemptActionPerMinute: "1000",
    Exam__ExpirationSweepIntervalSeconds: "5",
    "Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command": "Warning",
  },
});

const stop = () => api.kill();
process.on("SIGINT", stop);
process.on("SIGTERM", stop);
api.on("exit", (code) => process.exit(code ?? 0));
