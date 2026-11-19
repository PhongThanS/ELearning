import { useState } from "react";
import { Button, Card } from "react-bootstrap";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { usersApi } from "../../services/api";
import { ConfirmDialog } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { SearchableSelect } from "../../components/common/SearchableSelect";
import type { Classroom } from "../../types/api";

/** Lấy hết học viên của một lớp (mỗi trang tối đa 100) để tích chọn cả lớp. */
async function fetchClassStudents(
  classroomId: string,
): Promise<{ id: string; name: string }[]> {
  const result: { id: string; name: string }[] = [];
  for (let page = 1; page <= 50; page++) {
    const data = await usersApi.list({
      classroomId,
      roleCode: "STUDENT",
      page,
      pageSize: 100,
      sortBy: "userName",
      sortDir: "asc",
    });
    result.push(
      ...data.items.map((u) => ({ id: u.id, name: u.fullName || u.userName })),
    );
    if (page >= data.totalPages) break;
  }
  return result;
}

/**
 * Thanh khóa / mở khóa hàng loạt (D-40) ở trang Người dùng: tích từng tài khoản trong bảng hoặc tích cả lớp,
 * rồi bấm Khóa / Mở khóa. Tài khoản của chính mình luôn được bỏ qua.
 */
export function UserBulkStatusBar({
  selected,
  pickedClassId,
  onPickClass,
  onClear,
  classrooms,
  classroomsLoading,
}: {
  selected: Map<string, string>;
  /** Lớp đang được tích (chỉ một lớp); "" = không tích theo lớp. */
  pickedClassId: string;
  /** Tích theo lớp: thay toàn bộ lựa chọn bằng học viên của lớp (classId "" = bỏ). */
  onPickClass: (classId: string, users: { id: string; name: string }[]) => void;
  onClear: () => void;
  classrooms: Classroom[];
  classroomsLoading: boolean;
}) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [action, setAction] = useState<"lock" | "unlock" | null>(null);
  const [loadingClass, setLoadingClass] = useState(false);

  const pickClass = async (classroomId: string) => {
    if (!classroomId) {
      onPickClass("", []);
      return;
    }
    setLoadingClass(true);
    try {
      const students = await fetchClassStudents(classroomId);
      const name = classrooms.find((c) => c.id === classroomId)?.name ?? "";
      if (students.length === 0) {
        onPickClass(classroomId, []);
        toast.info(`Lớp ${name} chưa có học viên.`);
      } else {
        onPickClass(classroomId, students);
        toast.success(`Đã tích ${students.length} học viên của lớp ${name}.`);
      }
    } catch (e) {
      toast.error(e);
    } finally {
      setLoadingClass(false);
    }
  };

  const run = useMutation({
    mutationFn: ({ isActive, reason }: { isActive: boolean; reason: string }) =>
      usersApi.bulkSetStatus(
        [...selected.keys()],
        isActive,
        reason || undefined,
      ),
    onSuccess: (r, { isActive }) => {
      const verb = isActive ? "Đã mở khóa" : "Đã khóa";
      const extra = [
        r.unchanged > 0 && `${r.unchanged} tài khoản đã ở trạng thái này`,
        r.skipped > 0 && `bỏ qua ${r.skipped}`,
      ]
        .filter(Boolean)
        .join(", ");
      toast.success(
        `${verb} ${r.changed} tài khoản${extra ? ` (${extra})` : ""}.`,
      );
      setAction(null);
      onClear();
      void queryClient.invalidateQueries({ queryKey: ["users"] });
    },
    onError: (e) => toast.error(e),
  });

  const count = selected.size;
  const names = [...selected.values()];

  return (
    <Card className="mb-2 border-0 bg-body-tertiary">
      <Card.Body className="py-2 px-3 d-flex flex-wrap align-items-center gap-2">
        <span className="small fw-semibold text-secondary">
          <i className="bi bi-check2-square me-1" aria-hidden="true" />
          Khóa hàng loạt:
        </span>
        <div style={{ minWidth: 240 }}>
          <SearchableSelect
            id="users-bulk-class"
            size="sm"
            value={pickedClassId}
            onChange={(val) => void pickClass(val)}
            clearable
            options={classrooms.map((c) => ({
              value: c.id,
              label: c.name,
              code: c.code,
              sublabel: `${c.studentCount} học viên`,
            }))}
            placeholder={
              loadingClass ? "Đang lấy học viên..." : "Tích cả lớp..."
            }
            searchPlaceholder="Tìm lớp theo mã hoặc tên..."
            loading={classroomsLoading || loadingClass}
          />
        </div>
        <span className="small ms-1" aria-live="polite">
          {count > 0 ? (
            <>
              Đã chọn <strong>{count}</strong> tài khoản
            </>
          ) : (
            <span className="text-secondary">
              Tích ô ở đầu dòng hoặc chọn lớp để tích cả lớp
            </span>
          )}
        </span>
        <div className="ms-auto d-flex gap-2">
          <Button
            size="sm"
            variant="danger"
            disabled={count === 0 || run.isPending}
            onClick={() => setAction("lock")}
          >
            <i className="bi bi-lock me-1" aria-hidden="true" />
            Khóa
          </Button>
          <Button
            size="sm"
            variant="success"
            disabled={count === 0 || run.isPending}
            onClick={() => setAction("unlock")}
          >
            <i className="bi bi-unlock me-1" aria-hidden="true" />
            Mở khóa
          </Button>
          <Button
            size="sm"
            variant="outline-secondary"
            disabled={count === 0 || run.isPending}
            onClick={onClear}
          >
            Bỏ chọn
          </Button>
        </div>
      </Card.Body>
      <ConfirmDialog
        show={action !== null}
        title={
          action === "lock"
            ? `Khóa ${count} tài khoản`
            : `Mở khóa ${count} tài khoản`
        }
        body={
          <>
            <p className="mb-2">
              {action === "lock"
                ? "Các tài khoản sẽ không đăng nhập được và bị đăng xuất khỏi mọi thiết bị. Khóa quá 90 ngày sẽ bị XÓA VĨNH VIỄN cùng bài thi, điểm, điểm danh."
                : "Các tài khoản sẽ đăng nhập lại được."}{" "}
              Tài khoản của chính bạn luôn được bỏ qua.
            </p>
            <div
              className="small text-secondary"
              style={{ maxHeight: 120, overflowY: "auto" }}
            >
              {names.slice(0, 30).join(", ")}
              {names.length > 30 && ` và ${names.length - 30} tài khoản khác`}
            </div>
          </>
        }
        confirmText={action === "lock" ? "Khóa" : "Mở khóa"}
        variant={action === "lock" ? "danger" : "success"}
        requireReason={action === "lock"}
        busy={run.isPending}
        onCancel={() => setAction(null)}
        onConfirm={(reason) =>
          run.mutate({ isActive: action === "unlock", reason })
        }
      />
    </Card>
  );
}
