#!/usr/bin/env bash
# Chạy kịch bản k6 và lưu tóm tắt vào load-tests/results/<kịch bản>-<thời điểm UTC>.json (artifact CI / hồ sơ release).
#   load-tests/run.sh exam-start-burst
#   BASE_URL=https://staging.example.vn ADMIN_PASSWORD='...' load-tests/run.sh all
#   SMOKE=1 VUS=10 DURATION=1m load-tests/run.sh all          # kiểm tra nhanh kịch bản, không đo hiệu năng
# Dùng k6 cài trên máy nếu có, không thì chạy image grafana/k6 (cần Docker). Tham số thừa được chuyển cho `k6 run`.
set -euo pipefail

cd "$(dirname "$0")"
ALL=(exam-start-burst exam-steady exam-submit-wave expiry-sweep)
K6_IMAGE="${K6_IMAGE:-grafana/k6:1.3.0}"
# Biến cấu hình được chuyển vào container (xem README.md)
VARS=(BASE_URL ADMIN_USER ADMIN_PASSWORD USER_PREFIX GROUP_CODE STUDENT_PASSWORD VUS QUESTION_COUNT SMOKE
  START_WINDOW_SECONDS DOUBLE_START DURATION RAMP_SECONDS MIN_THINK_SECONDS MAX_THINK_SECONDS
  PREP_SECONDS WAVE_SECONDS LEAD_SECONDS POLL_SECONDS SWEEP_TIMEOUT_SECONDS)

name="${1:?Dùng: load-tests/run.sh <tên kịch bản|all> [tham số k6...]}"
shift
if [[ "$name" == all ]]; then
  scenarios=("${ALL[@]}")
elif [[ -f "scenarios/$name.js" ]]; then
  scenarios=("$name")
else
  echo "Không có kịch bản '$name'. Chọn: ${ALL[*]} hoặc all." >&2
  exit 2
fi

mkdir -p results
k6() {
  if command -v k6 >/dev/null 2>&1; then
    command k6 "$@"
  else
    local env_args=()
    for v in "${VARS[@]}"; do
      if [[ -n "${!v:-}" ]]; then env_args+=(-e "$v"); fi
    done
    docker run --rm -i --network host -u "$(id -u):$(id -g)" -v "$PWD:/load-tests" -w /load-tests \
      -e K6_NO_USAGE_REPORT=true "${env_args[@]}" "$K6_IMAGE" "$@"
  fi
}

failed=()
for s in "${scenarios[@]}"; do
  out="results/$s-$(date -u +%Y%m%dT%H%M%SZ).json"
  echo "=== $s → $out"
  if ! k6 run --summary-export "$out" "$@" "scenarios/$s.js"; then
    failed+=("$s")
  fi
done

if (( ${#failed[@]} > 0 )); then
  echo "Không đạt: ${failed[*]}" >&2
  exit 1
fi
echo "Đạt: ${scenarios[*]}"
