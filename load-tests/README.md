# Load test (k6)

Kiểm chứng chỉ tiêu phi chức năng ở [`docs/01-tong-quan.md`](../docs/01-tong-quan.md) mục 6. Bảng kịch bản và ngưỡng: [`docs/08-kiem-thu.md`](../docs/08-kiem-thu.md) mục 7.

| Kịch bản | Làm gì | Ngưỡng |
|---|---|---|
| `exam-start-burst` | `VUS` học viên đăng nhập rồi `start` rải trong `START_WINDOW_SECONDS` (60 s); mỗi người start lần hai phải nhận lại đúng lượt cũ | p95 start < 1 s; không 5xx; mỗi học viên đúng 1 lượt |
| `exam-steady` | `VUS` học viên lưu đáp án mỗi 5–15 s trong `DURATION` (30 phút); 10% là lô vài câu, 5% kèm sự kiện đổi tab | p95 lưu đáp án < 300 ms; lỗi < 0.1% |
| `exam-submit-wave` | Chuẩn bị (`PREP_SECONDS`), rồi `VUS` học viên nộp rải trong `WAVE_SECONDS` (2 phút); nộp lại lần hai phải ra cùng kết quả | p95 submit < 2 s |
| `expiry-sweep` | `VUS` lượt thi cùng hết giờ (đề có `EndAt`), không ai nộp; một VU quan sát tới khi job tự nộp hết | Lượt cuối được tự nộp < 5 phút sau `EndAt` |

Mọi kịch bản còn có ngưỡng về tính đúng: không có 5xx (`server_errors`), không có lượt thi sai (`integrity_errors`), mọi `check` đạt.

## Chuẩn bị

- Chạy trên **staging có cấu hình giống production** (docs/08 mục 7), đi qua Nginx như học viên thật. Không chạy trên production: kịch bản tạo học viên, câu hỏi và đề thi.
- Tài khoản admin đã đổi mật khẩu khởi tạo (production bắt buộc đổi ở lần đăng nhập đầu).
- k6 ≥ 1.0 cài trên máy, hoặc Docker (script tự dùng image `grafana/k6`).
- Máy chạy k6 nằm sau **một IP**, như một phòng thi dùng chung NAT: giới hạn `auth-login` 600 / phút / IP phải đủ cho 500 lượt đăng nhập trong 60 giây (docs/07 mục 5). Nếu thấy `rate_limited` > 0 thì đó là kết quả cần báo lại, không phải lỗi kịch bản.

Dữ liệu do `setup()` của mỗi kịch bản tạo qua API admin:

- Nhóm `GROUP_CODE` (mặc định `LOADTEST`) với học viên `lt.hv.0001` … `lt.hv.<VUS>`, mật khẩu `STUDENT_PASSWORD`. Học viên được dùng lại giữa các lần chạy; lần đầu tạo 500 người mất vài phút vì rate limit của admin (tự chờ theo `Retry-After`).
- Mỗi lần chạy một đề mới `LT-<KỊCH BẢN>-<mã>` gồm `QUESTION_COUNT` câu (mặc định 20, đủ 4 loại), 1 lượt thi, gán cho nhóm và đã publish. Vì vậy số lượt không dồn qua các lần chạy.

## Chạy

```bash
# Một kịch bản
BASE_URL=https://staging.example.vn ADMIN_PASSWORD='<mật khẩu admin>' load-tests/run.sh exam-start-burst

# Cả 4 kịch bản theo thứ tự (khoảng 45 phút với tham số mặc định)
BASE_URL=https://staging.example.vn ADMIN_PASSWORD='<mật khẩu admin>' load-tests/run.sh all

# Kiểm tra nhanh kịch bản (không đo hiệu năng): ít VU, thời gian ngắn, bỏ ngưỡng độ trễ
SMOKE=1 VUS=10 DURATION=1m START_WINDOW_SECONDS=10 PREP_SECONDS=30 WAVE_SECONDS=15 LEAD_SECONDS=10 \
  BASE_URL=http://localhost:8080 ADMIN_PASSWORD='<mật khẩu admin>' load-tests/run.sh all
```

Tóm tắt của mỗi lần chạy nằm ở `load-tests/results/<kịch bản>-<thời điểm UTC>.json` (không commit). Tham số thừa sau tên kịch bản được chuyển cho `k6 run`, ví dụ `load-tests/run.sh exam-steady --out json=results/steady-raw.json`.

CI (`.github/workflows/docker.yml`) chạy cả 4 kịch bản ở chế độ `SMOKE=1` với 10 VU trên stack Docker Compose, để kịch bản luôn khớp với API.

## Biến môi trường

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `BASE_URL` | `http://localhost:8080` | Origin công khai (Nginx) |
| `ADMIN_USER` / `ADMIN_PASSWORD` | `admin` / `Admin@123456` | Tài khoản dựng dữ liệu (mặc định là tài khoản dev) |
| `VUS` | `500` | Số học viên ảo |
| `USER_PREFIX` / `GROUP_CODE` / `STUDENT_PASSWORD` | `lt.hv.` / `LOADTEST` / `LoadTest@2026` | Học viên tải |
| `QUESTION_COUNT` | `20` | Số câu của đề (tối đa 50) |
| `SMOKE` | — | `1`: bỏ ngưỡng độ trễ, chỉ giữ ngưỡng về tính đúng |
| `START_WINDOW_SECONDS` | `60` | Cửa sổ đăng nhập + start (`exam-start-burst`, `expiry-sweep`) |
| `DOUBLE_START` | — | `1`: lần start đầu gửi hai request song song, phải ra đúng một lượt mới (`exam-start-burst`) |
| `DURATION` / `RAMP_SECONDS` | `30m` / `60` | Thời gian chạy và cửa sổ vào thi (`exam-steady`) |
| `MIN_THINK_SECONDS` / `MAX_THINK_SECONDS` | `5` / `15` | Khoảng giữa hai lần lưu (`exam-steady`) |
| `PREP_SECONDS` / `WAVE_SECONDS` | `120` / `120` | Pha chuẩn bị và cửa sổ nộp (`exam-submit-wave`) |
| `LEAD_SECONDS` | `60` | Khoảng từ cuối cửa sổ start tới `EndAt` (`expiry-sweep`) |
| `POLL_SECONDS` / `SWEEP_TIMEOUT_SECONDS` | `5` / `600` | Chu kỳ kiểm tra và thời gian chờ tối đa của job (`expiry-sweep`) |

## Đọc kết quả

- `http_req_duration{name:start|save_answers|submit}`: độ trễ theo loại request; ngưỡng p95 ở bảng trên.
- `server_errors`: số response 5xx hoặc lỗi kết nối.
- `integrity_errors`: start lần hai ra lượt khác, lưu đáp án không được áp dụng, nộp lại ra kết quả khác, thiếu / thừa lượt thi, lượt không được tự nộp.
- `rate_limited`: số response 429 (API hoặc Nginx).
- `expiry_sweep_seconds`: `max(SubmittedAt) − EndAt` tính bằng giờ server; gồm 30 giây ân hạn và tối đa 60 giây chờ vòng quét.

Ghi kết quả của lần chạy trước release vào hồ sơ release (hoặc giữ artifact CI), kèm cấu hình máy chủ và phiên bản.
